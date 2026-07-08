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
        var raw = await db.OperadorasCartao.AsNoTracking()
            .Where(o => o.EmpresaId == empresaId && o.Ativo)
            .OrderBy(o => o.Nome)
            .ToListAsync(ct);

        var lista = raw.Select(o => new
        {
            o.Id, o.Nome, o.Cor, o.Icone, o.Ativo, o.Observacao,
            o.TaxaDebito, o.TaxaCreditoVista, o.TaxaCreditoParcelado,
            o.TaxaPix, o.TaxaAntecipacao,
            prazoDebito = o.PrazoDiasDebito,
            prazoCreditoVista = o.PrazoDiasCreditoVista,
            prazoCreditoParcelado = o.PrazoDiasCreditoParcelado,
            prazoPix = o.PrazoDiasPix,
            bandeiras = string.IsNullOrEmpty(o.BandeirasJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(o.BandeirasJson) ?? new List<string>(),
        });
        return Ok(lista);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] OperadoraCartaoRequest req, CancellationToken ct)
    {
        var op = OperadoraCartao.Criar(req.EmpresaId, req.Nome, req.Cor, req.Icone,
            req.TaxaDebito, req.TaxaCreditoVista, req.TaxaCreditoParcelado,
            req.PrazoDebito, req.PrazoCreditoVista, req.PrazoCreditoParcelado,
            req.Bandeiras, req.TaxaPix, req.PrazoPix, req.TaxaAntecipacao, req.Observacao);
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
            req.PrazoDebito, req.PrazoCreditoVista, req.PrazoCreditoParcelado,
            req.Bandeiras, req.TaxaPix, req.PrazoPix, req.TaxaAntecipacao, req.Observacao);
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
    int PrazoDebito, int PrazoCreditoVista, int PrazoCreditoParcelado,
    List<string>? Bandeiras = null,
    decimal TaxaPix = 0, int PrazoPix = 0, decimal TaxaAntecipacao = 0,
    string? Observacao = null);
