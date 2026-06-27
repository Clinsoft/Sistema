using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Marketing.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Marketing;

[ApiController]
[Route("api/marketing")]
[Authorize]
public class MarketingController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    // ─── Templates ────────────────────────────────────────────────────────────

    [HttpGet("templates")]
    public async Task<IActionResult> ListarTemplates([FromQuery] Guid empresaId,
        [FromQuery] string? tipo, CancellationToken ct)
    {
        var query = db.TemplatesMarketing.AsNoTracking()
            .Where(t => (t.EmpresaId == empresaId || t.EhTemplate) && t.Ativo);

        if (!string.IsNullOrEmpty(tipo) && Enum.TryParse<TipoArteMarketing>(tipo, out var tipoEnum))
            query = query.Where(t => t.Tipo == tipoEnum);

        return Ok(await query.OrderBy(t => t.Nome).ToListAsync(ct));
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CriarTemplate([FromBody] CriarTemplateRequest req, CancellationToken ct)
    {
        var tipo = Enum.Parse<TipoArteMarketing>(req.Tipo);
        var formato = Enum.Parse<FormatoArte>(req.Formato);
        var template = TemplateMarketing.Criar(req.EmpresaId, req.Nome, tipo, formato, req.LayoutJson);
        db.TemplatesMarketing.Add(template);
        await uow.SalvarAsync(ct);
        return Ok(new { template.Id });
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> AtualizarTemplate(Guid id, [FromBody] AtualizarTemplateRequest req, CancellationToken ct)
    {
        var template = await db.TemplatesMarketing.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Template não encontrado.");
        template.AtualizarLayout(req.LayoutJson, req.ThumbnailBase64);
        db.TemplatesMarketing.Update(template);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // ─── Artes ────────────────────────────────────────────────────────────────

    [HttpGet("artes")]
    public async Task<IActionResult> ListarArtes([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.ArtesMarketing.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId)
            .OrderByDescending(a => a.CriadoEm)
            .ToListAsync(ct));

    [HttpPost("artes")]
    public async Task<IActionResult> SalvarArte([FromBody] SalvarArteRequest req, CancellationToken ct)
    {
        // Herda tipo/formato do template
        var template = await db.TemplatesMarketing.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == req.TemplateId, ct);

        var tipo = template?.Tipo ?? TipoArteMarketing.Generico;
        var formato = template?.Formato ?? FormatoArte.FeedQuadrado;

        var arte = ArteMarketing.Criar(req.EmpresaId, req.Nome, tipo, formato, req.LayoutJson, req.TemplateId);
        db.ArtesMarketing.Add(arte);
        await uow.SalvarAsync(ct);
        return Ok(new { arte.Id });
    }

    // ─── Agendamentos ─────────────────────────────────────────────────────────

    [HttpGet("agendamentos")]
    public async Task<IActionResult> ListarAgendamentos([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.AgendamentosPublicacao.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId)
            .OrderBy(a => a.DataHoraAgendada)
            .ToListAsync(ct));

    [HttpPost("agendamentos")]
    public async Task<IActionResult> Agendar([FromBody] AgendarPublicacaoRequest req, CancellationToken ct)
    {
        var rede = Enum.Parse<RedesSociais>(req.RedeSocial);
        var agendamento = AgendamentoPublicacao.Criar(req.EmpresaId, req.ArteId, rede, req.DataHoraAgendada, req.Legenda);
        db.AgendamentosPublicacao.Add(agendamento);
        await uow.SalvarAsync(ct);
        return Ok(new { agendamento.Id });
    }
}

public record CriarTemplateRequest(Guid EmpresaId, string Nome, string Tipo, string Formato, string LayoutJson);
public record AtualizarTemplateRequest(string LayoutJson, string? ThumbnailBase64 = null);
public record SalvarArteRequest(Guid EmpresaId, string Nome, Guid TemplateId, string LayoutJson);
public record AgendarPublicacaoRequest(Guid EmpresaId, Guid ArteId, string RedeSocial, DateTime DataHoraAgendada, string? Legenda = null);
