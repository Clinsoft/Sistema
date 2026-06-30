using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/financeiro/recebiveis-cartao")]
[Authorize]
public class ReceiveisCartaoController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] Guid? operadoraId,
        [FromQuery] string? status,
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        CancellationToken ct)
    {
        var query = db.ReceiveisCartao.AsNoTracking()
            .Include(r => r.Operadora)
            .Where(r => r.EmpresaId == empresaId);

        if (operadoraId.HasValue)
            query = query.Where(r => r.OperadoraCartaoId == operadoraId.Value);

        if (Enum.TryParse<StatusRecebivelCartao>(status, out var st))
            query = query.Where(r => r.Status == st);

        if (inicio.HasValue) query = query.Where(r => r.DataTransacao >= inicio.Value);
        if (fim.HasValue)    query = query.Where(r => r.DataTransacao <= fim.Value);

        var lista = await query
            .OrderByDescending(r => r.DataPrevistaRepasse)
            .Take(200)
            .Select(r => new
            {
                r.Id, r.VendaId, r.FormaPagamento, r.Parcelas,
                r.ValorBruto, r.Taxa, r.ValorLiquido,
                r.DataTransacao, r.DataPrevistaRepasse, r.DataRepasse, r.DataAntecipacao,
                r.TaxaAntecipacaoAplicada, r.NsuTid,
                Status = r.Status.ToString(),
                operadoraNome = r.Operadora != null ? r.Operadora.Nome : "",
                operadoraCor   = r.Operadora != null ? r.Operadora.Cor : null,
            })
            .ToListAsync(ct);

        // Cards de resumo
        var resumo = await db.ReceiveisCartao.AsNoTracking()
            .Where(r => r.EmpresaId == empresaId)
            .GroupBy(r => 1)
            .Select(g => new
            {
                totalPendente   = g.Where(r => r.Status == StatusRecebivelCartao.Pendente).Sum(r => r.ValorLiquido),
                totalRecebido   = g.Where(r => r.Status == StatusRecebivelCartao.Recebido).Sum(r => r.ValorLiquido),
                totalAntecipado = g.Where(r => r.Status == StatusRecebivelCartao.Antecipado).Sum(r => r.ValorLiquido),
                totalTaxas      = g.Sum(r => r.ValorBruto - r.ValorLiquido),
            })
            .FirstOrDefaultAsync(ct);

        return Ok(new { resumo, recebiveis = lista });
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarRecebivelRequest req, CancellationToken ct)
    {
        var recebivel = RecebivelCartao.Criar(
            req.EmpresaId, req.OperadoraCartaoId, req.VendaId,
            req.FormaPagamento, req.Parcelas, req.ValorBruto, req.Taxa,
            req.DataTransacao, req.DataPrevistaRepasse, req.NsuTid);
        db.ReceiveisCartao.Add(recebivel);
        await db.SaveChangesAsync(ct);
        return Ok(new { recebivel.Id, recebivel.ValorLiquido });
    }

    [HttpPost("receber")]
    public async Task<IActionResult> MarcarRecebido([FromBody] IdsRequest req, CancellationToken ct)
    {
        var itens = await db.ReceiveisCartao
            .Where(r => req.Ids.Contains(r.Id) && r.Status == StatusRecebivelCartao.Pendente)
            .ToListAsync(ct);
        foreach (var r in itens) r.MarcarRecebido();
        await db.SaveChangesAsync(ct);
        return Ok(new { atualizados = itens.Count });
    }

    [HttpPost("antecipar")]
    public async Task<IActionResult> Antecipar([FromBody] AnteciparRequest req, CancellationToken ct)
    {
        var itens = await db.ReceiveisCartao
            .Where(r => req.Ids.Contains(r.Id) && r.Status == StatusRecebivelCartao.Pendente)
            .ToListAsync(ct);
        foreach (var r in itens) r.MarcarAntecipado(req.TaxaAntecipacao);
        await db.SaveChangesAsync(ct);
        return Ok(new { atualizados = itens.Count });
    }
}

public record RegistrarRecebivelRequest(
    Guid EmpresaId, Guid OperadoraCartaoId, Guid? VendaId,
    string FormaPagamento, int Parcelas, decimal ValorBruto, decimal Taxa,
    DateTime DataTransacao, DateTime DataPrevistaRepasse, string? NsuTid = null);

public record IdsRequest(List<Guid> Ids);
public record AnteciparRequest(List<Guid> Ids, decimal TaxaAntecipacao);
