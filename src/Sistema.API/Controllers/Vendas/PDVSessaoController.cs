using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Vendas.Commands;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Vendas;

[ApiController]
[Route("api/pdv/sessoes")]
[Authorize]
public class PDVSessaoController(IMediator mediator, IPDVSessaoRepository repo, SistemaDbContext db) : ControllerBase
{
    /// <summary>Abre uma nova sessão de caixa.</summary>
    [HttpPost("abrir")]
    public async Task<IActionResult> Abrir([FromBody] AbrirSessaoCommand cmd, CancellationToken ct)
    {
        // O usuário é o AUTENTICADO (do token), nunca o que o cliente envia. Se o
        // frontend mandar vazio, a sessão ficava com UsuarioId zerado e a verificação
        // (que usa o usuário do token) nunca a encontrava → "abra o caixa" em loop.
        if (cmd.UsuarioId == Guid.Empty &&
            Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid))
            cmd = cmd with { UsuarioId = uid };

        if (cmd.UsuarioId == Guid.Empty)
            return BadRequest(new { mensagem = "Usuário não identificado. Faça login novamente." });

        var id = await mediator.Send(cmd, ct);
        return Ok(new { id });
    }

    /// <summary>Fecha a sessão de caixa com o saldo contado.</summary>
    [HttpPost("{id:guid}/fechar")]
    public async Task<IActionResult> Fechar(Guid id, [FromBody] FecharRequest req, CancellationToken ct)
    {
        var resultado = await mediator.Send(new FecharSessaoCommand(id, req.SaldoFechamento, req.Observacao), ct);
        return Ok(resultado);
    }

    /// <summary>Retorna a sessão aberta do usuário atual.</summary>
    [HttpGet("aberta")]
    public async Task<IActionResult> SessaoAberta([FromQuery] Guid empresaId, [FromQuery] Guid? usuarioId, CancellationToken ct)
    {
        var uid = usuarioId
            ?? (Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var cid) ? cid : Guid.Empty);
        var sessao = await repo.ObterSessaoAbertaAsync(empresaId, uid, ct);
        if (sessao is null) return Ok(null);

        var numeros = await NumerosPorSessaoAsync(empresaId, ct);
        var operador = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == sessao.UsuarioId).Select(u => u.Nome).FirstOrDefaultAsync(ct);
        var local = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.Id == sessao.LocalEstoqueId).Select(l => l.Nome).FirstOrDefaultAsync(ct);
        var b = await BreakdownFormasAsync(sessao, ct);
        var operacoes = await db.OperacoesCaixa.AsNoTracking()
            .Where(o => o.SessaoId == sessao.Id)
            .OrderBy(o => o.CriadoEm)
            .Select(o => new { tipo = o.Tipo.ToString(), o.Valor, o.Descricao, o.CriadoEm })
            .ToListAsync(ct);

        return Ok(new
        {
            sessao.Id,
            numero = numeros.GetValueOrDefault(sessao.Id, 1),
            abertura = sessao.Abertura,
            operador,
            localEstoque = local,
            sessao.UsuarioId, sessao.LocalEstoqueId,
            sessao.SaldoAbertura, sessao.TotalVendas,
            sessao.TotalSuprimentos, sessao.TotalSangrias,
            totalDinheiro = b.Dinheiro, totalPix = b.Pix,
            totalCartaoCredito = b.Credito, totalCartaoDebito = b.Debito,
            operacoes,
            sessao.Status
        });
    }

    /// <summary>Lista sessões por período (para relatório de fechamentos).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        CancellationToken ct)
    {
        var sessoes = (await repo.ListarPorPeriodoAsync(empresaId, inicio, fim, ct)).ToList();

        var numeros = await NumerosPorSessaoAsync(empresaId, ct);
        var usuarioIds = sessoes.Select(s => s.UsuarioId).Distinct().ToList();
        var localIds = sessoes.Select(s => s.LocalEstoqueId).Distinct().ToList();
        var operadores = await db.Usuarios.AsNoTracking()
            .Where(u => usuarioIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nome, ct);
        var locais = await db.LocaisEstoque.AsNoTracking()
            .Where(l => localIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        var lista = new List<object>();
        foreach (var s in sessoes)
        {
            var b = await BreakdownFormasAsync(s, ct);
            lista.Add(new
            {
                s.Id,
                numero = numeros.GetValueOrDefault(s.Id, 0),
                s.UsuarioId,
                operador = operadores.GetValueOrDefault(s.UsuarioId),
                localEstoque = locais.GetValueOrDefault(s.LocalEstoqueId),
                s.Abertura, s.Fechamento,
                s.SaldoAbertura, s.SaldoFechamento,
                s.TotalVendas, s.TotalSuprimentos, s.TotalSangrias,
                totalDinheiro = b.Dinheiro, totalPix = b.Pix,
                totalCartaoCredito = b.Credito, totalCartaoDebito = b.Debito,
                s.Status, s.ObservacaoFechamento
            });
        }
        return Ok(lista);
    }

    /// <summary>Número sequencial da sessão por empresa (ordem de abertura), já que não há coluna Numero.</summary>
    private async Task<Dictionary<Guid, int>> NumerosPorSessaoAsync(Guid empresaId, CancellationToken ct)
    {
        var ordenadas = await db.PDVSessoes.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId)
            .OrderBy(s => s.Abertura)
            .Select(s => s.Id)
            .ToListAsync(ct);
        return ordenadas.Select((id, i) => (id, n: i + 1)).ToDictionary(x => x.id, x => x.n);
    }

    /// <summary>Soma dos pagamentos finalizados na janela da sessão, por forma.</summary>
    private async Task<(decimal Dinheiro, decimal Pix, decimal Credito, decimal Debito)> BreakdownFormasAsync(
        PDVSessao s, CancellationToken ct)
    {
        var grupos = await db.PagamentosVenda.AsNoTracking()
            .Join(db.Vendas, p => p.VendaId, v => v.Id,
                (p, v) => new { p.Forma, p.Valor, v.Status, v.DataHora, v.EmpresaId })
            .Where(x => x.EmpresaId == s.EmpresaId
                && x.Status == StatusVenda.Finalizada
                && x.DataHora >= s.Abertura
                && (s.Fechamento == null || x.DataHora <= s.Fechamento))
            .GroupBy(x => x.Forma)
            .Select(g => new { forma = g.Key, total = g.Sum(x => x.Valor) })
            .ToListAsync(ct);

        decimal Get(FormaPagamento f) => grupos.FirstOrDefault(g => g.forma == f)?.total ?? 0m;
        return (Get(FormaPagamento.Dinheiro), Get(FormaPagamento.Pix),
                Get(FormaPagamento.CartaoCredito), Get(FormaPagamento.CartaoDebito));
    }
}

public record FecharRequest(decimal SaldoFechamento, string? Observacao = null);
