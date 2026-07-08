using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Marketing.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Marketing;

[ApiController]
[Route("api/promocoes")]
[Authorize]
public class PromocoesController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Lista as promoções da empresa, com filtros de tipo, status (calculado) e texto.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] string? tipo,
        [FromQuery] string? status,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var query = db.Promocoes.AsNoTracking().Where(p => p.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(p => p.Tipo == tipo);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Nome.Contains(q));

        var lista = await query.OrderByDescending(p => p.CriadoEm).ToListAsync(ct);

        var resultado = lista.Select(p => new
        {
            p.Id, p.Nome, p.Tipo, p.TipoDesconto, p.Desconto,
            p.DataInicio, p.DataFim, p.AplicaEm, p.ReferenciaId,
            p.QtdeLeve, p.QtdePague, p.ValorMinimoPedido, p.LimiteUso,
            p.ApenasClube, p.Cumulativo, p.QtdeUsada,
            status = p.StatusCalculado(),
        });

        // Filtro por status (calculado) aplicado em memória
        if (!string.IsNullOrWhiteSpace(status))
            resultado = resultado.Where(x => x.status == status);

        return Ok(resultado.ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] PromocaoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            return BadRequest(new { mensagem = "Nome é obrigatório." });

        var promo = Promocao.Criar(
            req.EmpresaId, req.Nome, req.Tipo ?? "Desconto", req.TipoDesconto ?? "Percentual",
            req.Desconto, ParseData(req.DataInicio) ?? DateTime.Today, ParseData(req.DataFim),
            req.AplicaEm ?? "Todos", req.ReferenciaId,
            req.QtdeLeve, req.QtdePague, req.ValorMinimoPedido, req.LimiteUso,
            req.ApenasClube, req.Cumulativo);

        db.Promocoes.Add(promo);
        await db.SaveChangesAsync(ct);
        return Ok(new { promo.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] PromocaoRequest req, CancellationToken ct)
    {
        var promo = await db.Promocoes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Promoção não encontrada.");

        promo.Editar(
            req.Nome, req.Tipo ?? "Desconto", req.TipoDesconto ?? "Percentual", req.Desconto,
            ParseData(req.DataInicio) ?? promo.DataInicio, ParseData(req.DataFim),
            req.AplicaEm ?? "Todos", req.ReferenciaId,
            req.QtdeLeve, req.QtdePague, req.ValorMinimoPedido, req.LimiteUso,
            req.ApenasClube, req.Cumulativo);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> AlterarStatus(Guid id, [FromBody] StatusRequest req, CancellationToken ct)
    {
        var promo = await db.Promocoes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Promoção não encontrada.");

        // "Ativa" reativa; qualquer outro valor (Encerrada) desativa
        promo.DefinirAtiva(string.Equals(req.Status, "Ativa", StringComparison.OrdinalIgnoreCase));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var promo = await db.Promocoes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Promoção não encontrada.");
        db.Promocoes.Remove(promo);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static DateTime? ParseData(string? s)
        => DateTime.TryParse(s, out var d) ? d : null;
}

public record PromocaoRequest(
    Guid EmpresaId,
    string Nome,
    string? Tipo,
    string? TipoDesconto,
    decimal Desconto,
    string? DataInicio,
    string? DataFim,
    string? AplicaEm,
    Guid? ReferenciaId,
    int QtdeLeve = 0,
    int QtdePague = 0,
    decimal ValorMinimoPedido = 0,
    int LimiteUso = 0,
    bool ApenasClube = false,
    bool Cumulativo = false);

public record StatusRequest(string Status);
