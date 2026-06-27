using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Vendas;

/// <summary>Suprimentos, sangrias, cancelamento de venda e fita de caixa.</summary>
[ApiController]
[Route("api/caixa")]
[Authorize]
public class OperacoesCaixaController(
    IPDVSessaoRepository sessaoRepo,
    IVendaRepository vendaRepo,
    SistemaDbContext db,
    IUnitOfWork uow) : ControllerBase
{
    /// <summary>Registra suprimento (entrada de dinheiro) na sessão.</summary>
    [HttpPost("sessoes/{sessaoId:guid}/suprimento")]
    public async Task<IActionResult> Suprimento(Guid sessaoId, [FromBody] OperacaoRequest req, CancellationToken ct)
    {
        var sessao = await sessaoRepo.ObterPorIdAsync(sessaoId, ct)
            ?? throw new KeyNotFoundException("Sessão não encontrada.");
        sessao.AdicionarSuprimento(req.Valor);
        sessaoRepo.Atualizar(sessao);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Registra sangria (retirada de dinheiro) da sessão.</summary>
    [HttpPost("sessoes/{sessaoId:guid}/sangria")]
    public async Task<IActionResult> Sangria(Guid sessaoId, [FromBody] OperacaoRequest req, CancellationToken ct)
    {
        var sessao = await sessaoRepo.ObterPorIdAsync(sessaoId, ct)
            ?? throw new KeyNotFoundException("Sessão não encontrada.");
        sessao.AdicionarSangria(req.Valor);
        sessaoRepo.Atualizar(sessao);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Cancela uma venda em aberto.</summary>
    [HttpPost("vendas/{vendaId:guid}/cancelar")]
    public async Task<IActionResult> CancelarVenda(Guid vendaId, [FromBody] CancelarRequest req, CancellationToken ct)
    {
        var venda = await vendaRepo.ObterComItensAsync(vendaId, ct)
            ?? throw new KeyNotFoundException("Venda não encontrada.");
        venda.Cancelar(req.Motivo);
        vendaRepo.Atualizar(venda);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Fita de caixa — log cronológico de todas as operações da sessão.</summary>
    [HttpGet("sessoes/{sessaoId:guid}/fita")]
    public async Task<IActionResult> FitaDeCaixa(Guid sessaoId, CancellationToken ct)
    {
        var sessao = await sessaoRepo.ObterPorIdAsync(sessaoId, ct)
            ?? throw new KeyNotFoundException("Sessão não encontrada.");

        var vendas = await db.Vendas.AsNoTracking()
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .Where(v => v.EmpresaId == sessao.EmpresaId
                && v.DataHora >= sessao.Abertura
                && (sessao.Fechamento == null || v.DataHora <= sessao.Fechamento))
            .OrderBy(v => v.DataHora)
            .ToListAsync(ct);

        var fita = vendas.Select(v => new
        {
            tipo = "Venda",
            v.Numero, v.DataHora, v.Status,
            v.Total, v.TotalPago, v.Troco,
            qtdItens = v.Itens.Count,
            pagamentos = v.Pagamentos.Select(p => new { p.Forma, p.Valor })
        }).ToList<object>();

        return Ok(new
        {
            sessao = new
            {
                sessao.Id, sessao.Abertura, sessao.Fechamento,
                sessao.SaldoAbertura, sessao.SaldoFechamento,
                sessao.TotalVendas, sessao.TotalSuprimentos,
                sessao.TotalSangrias, sessao.Status
            },
            operacoes = fita,
            totalVendas = vendas.Where(v => v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada).Sum(v => v.Total),
            ticketMedio = vendas.Any(v => v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
                ? vendas.Where(v => v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada).Average(v => v.Total)
                : 0
        });
    }

    /// <summary>Resumo de vendas por forma de pagamento da sessão.</summary>
    [HttpGet("sessoes/{sessaoId:guid}/resumo")]
    public async Task<IActionResult> Resumo(Guid sessaoId, CancellationToken ct)
    {
        var sessao = await sessaoRepo.ObterPorIdAsync(sessaoId, ct)
            ?? throw new KeyNotFoundException("Sessão não encontrada.");

        var pagamentos = await db.PagamentosVenda.AsNoTracking()
            .Join(db.Vendas,
                p => p.VendaId,
                v => v.Id,
                (p, v) => new { p.Forma, p.Valor, v.Status, v.DataHora, v.EmpresaId })
            .Where(x => x.EmpresaId == sessao.EmpresaId
                && x.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && x.DataHora >= sessao.Abertura
                && (sessao.Fechamento == null || x.DataHora <= sessao.Fechamento))
            .GroupBy(x => x.Forma)
            .Select(g => new { forma = g.Key.ToString(), total = g.Sum(x => x.Valor), qtd = g.Count() })
            .ToListAsync(ct);

        return Ok(new { sessaoId, resumoPorForma = pagamentos });
    }
}

public record OperacaoRequest(decimal Valor, string? Descricao = null);
public record CancelarRequest(string Motivo);
