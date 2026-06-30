using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;
using System.Text.Json;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/financeiro/operadoras-cartao")]
[Authorize]
public class OperadorasCartaoController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lista = await db.OperadorasCartao.AsNoTracking()
            .Where(o => o.EmpresaId == empresaId && o.Ativo)
            .OrderBy(o => o.Nome)
            .Select(o => new
            {
                o.Id, o.Nome, o.Cor, o.Icone, o.Ativo,
                o.TaxaDebito, o.TaxaCreditoVista, o.TaxaCreditoParcelado,
                o.TaxaPix, o.TaxaAntecipacao,
                prazoDebito = o.PrazoDiasDebito,
                prazoCreditoVista = o.PrazoDiasCreditoVista,
                prazoCreditoParcelado = o.PrazoDiasCreditoParcelado,
                prazoPix = o.PrazoDiasPix,
                bandeiras = o.BandeirasJson,
            })
            .ToListAsync(ct);
        return Ok(lista);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] OperadoraCartaoRequest req, CancellationToken ct)
    {
        var op = OperadoraCartao.Criar(req.EmpresaId, req.Nome, req.Cor, req.Icone,
            req.TaxaDebito, req.TaxaCreditoVista, req.TaxaCreditoParcelado,
            req.PrazoDiasDebito, req.PrazoDiasCreditoVista, req.PrazoDiasCreditoParcelado,
            req.Bandeiras, req.TaxaPix, req.PrazoDiasPix, req.TaxaAntecipacao);
        db.OperadorasCartao.Add(op);
        await db.SaveChangesAsync(ct);
        return Ok(new { op.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] OperadoraCartaoRequest req, CancellationToken ct)
    {
        var op = await db.OperadorasCartao.FindAsync([id], ct);
        if (op is null) return NotFound();
        op.Atualizar(req.Nome, req.Cor, req.Icone,
            req.TaxaDebito, req.TaxaCreditoVista, req.TaxaCreditoParcelado,
            req.PrazoDiasDebito, req.PrazoDiasCreditoVista, req.PrazoDiasCreditoParcelado,
            req.Bandeiras, req.TaxaPix, req.PrazoDiasPix, req.TaxaAntecipacao);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var op = await db.OperadorasCartao.FindAsync([id], ct);
        if (op is null) return NotFound();
        op.Desativar();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record OperadoraCartaoRequest(
    Guid EmpresaId, string Nome, string? Cor, string? Icone,
    decimal TaxaDebito, decimal TaxaCreditoVista, decimal TaxaCreditoParcelado,
    int PrazoDiasDebito, int PrazoDiasCreditoVista, int PrazoDiasCreditoParcelado,
    List<string>? Bandeiras = null,
    decimal TaxaPix = 0, int PrazoDiasPix = 0, decimal TaxaAntecipacao = 0);
