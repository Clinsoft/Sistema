using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Contabilidade.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Contabilidade;

[ApiController]
[Route("api/contabilidade/lancamentos")]
[Authorize(Roles = "Administrador,Contador")]
public class LancamentosContabeisController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var lancamentos = await db.LancamentosContabeis.AsNoTracking()
            .Include(l => l.Partidas)
            .Where(l => l.EmpresaId == empresaId
                && l.DataCompetencia >= inicio && l.DataCompetencia <= fim
                && !l.Estornado)
            .OrderBy(l => l.DataCompetencia).ThenBy(l => l.Numero)
            .ToListAsync(ct);
        return Ok(lancamentos);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var l = await db.LancamentosContabeis.Include(x => x.Partidas)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return l is null ? NotFound() : Ok(l);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarLancamentoContabilRequest req, CancellationToken ct)
    {
        // Gera número sequencial
        var seq = await db.LancamentosContabeis
            .CountAsync(l => l.EmpresaId == req.EmpresaId, ct) + 1;
        var numero = $"LC{DateTime.Now:yyyyMM}{seq:D5}";

        var tipo = Enum.Parse<TipoLancamentoContabil>(req.Tipo ?? "Normal");
        var lancamento = LancamentoContabil.Criar(req.EmpresaId, numero,
            req.DataCompetencia, req.Historico, tipo, req.DocumentoOrigem);

        foreach (var d in req.Debitos)
            lancamento.AdicionarDebito(d.ContaId, d.Valor, d.Complemento);
        foreach (var c in req.Creditos)
            lancamento.AdicionarCredito(c.ContaId, c.Valor, c.Complemento);

        if (!lancamento.EstaBalanceado())
            throw new InvalidOperationException("Lançamento não balanceado: débitos ≠ créditos.");

        db.LancamentosContabeis.Add(lancamento);
        await uow.SalvarAsync(ct);
        return Ok(new { lancamento.Id, lancamento.Numero });
    }

    [HttpPost("{id:guid}/estornar")]
    public async Task<IActionResult> Estornar(Guid id, CancellationToken ct)
    {
        var original = await db.LancamentosContabeis.Include(l => l.Partidas)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");

        if (original.Estornado)
            throw new InvalidOperationException("Lançamento já foi estornado.");

        var numero = $"EST{original.Numero}";
        var estorno = LancamentoContabil.Criar(original.EmpresaId, numero,
            DateTime.Today, $"ESTORNO: {original.Historico}",
            TipoLancamentoContabil.Estorno, original.DocumentoOrigem);

        // Inverte débitos e créditos
        foreach (var p in original.Partidas)
        {
            if (p.Natureza == NatureDebCred.Debito)
                estorno.AdicionarCredito(p.ContaContabilId, p.Valor);
            else
                estorno.AdicionarDebito(p.ContaContabilId, p.Valor);
        }

        db.LancamentosContabeis.Add(estorno);
        original.Estornar(estorno.Id);
        db.LancamentosContabeis.Update(original);
        await uow.SalvarAsync(ct);
        return Ok(new { estornoId = estorno.Id, estorno.Numero });
    }

    /// <summary>Balancete — saldo por conta no período.</summary>
    [HttpGet("balancete")]
    public async Task<IActionResult> Balancete([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var partidas = await db.PartidasContabeis.AsNoTracking()
            .Join(db.LancamentosContabeis, p => p.LancamentoContabilId, l => l.Id, (p, l) => new { p, l })
            .Where(x => x.l.EmpresaId == empresaId
                && x.l.DataCompetencia >= inicio && x.l.DataCompetencia <= fim
                && !x.l.Estornado)
            .Join(db.PlanoContas, x => x.p.ContaContabilId, c => c.Id,
                (x, c) => new
                {
                    c.Codigo, c.Nome, c.Natureza,
                    debito = x.p.Natureza == NatureDebCred.Debito ? x.p.Valor : 0m,
                    credito = x.p.Natureza == NatureDebCred.Credito ? x.p.Valor : 0m
                })
            .GroupBy(x => new { x.Codigo, x.Nome, x.Natureza })
            .Select(g => new
            {
                g.Key.Codigo, g.Key.Nome, g.Key.Natureza,
                totalDebitos = g.Sum(x => x.debito),
                totalCreditos = g.Sum(x => x.credito),
                saldo = g.Sum(x => x.debito) - g.Sum(x => x.credito)
            })
            .OrderBy(x => x.Codigo)
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            contas = partidas,
            totalDebitos = partidas.Sum(p => p.totalDebitos),
            totalCreditos = partidas.Sum(p => p.totalCreditos)
        });
    }
}

public record PartidaRequest(Guid ContaId, decimal Valor, string? Complemento = null);
public record CriarLancamentoContabilRequest(
    Guid EmpresaId, DateTime DataCompetencia, string Historico,
    IList<PartidaRequest> Debitos, IList<PartidaRequest> Creditos,
    string? Tipo = null, string? DocumentoOrigem = null);
