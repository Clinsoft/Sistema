using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers;

/// <summary>Log de auditoria: quem criou/alterou/excluiu cada registro.</summary>
[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = "Administrador,Gerente")]
public class AuditoriaController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] Guid? usuarioId, [FromQuery] string? entidade, [FromQuery] string? acao,
        [FromQuery] string? termo, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50,
        CancellationToken ct = default)
    {
        var fimExcl = fim.Date.AddDays(1);
        var q = db.AuditLogs.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.DataHora >= inicio.Date && a.DataHora < fimExcl);

        if (usuarioId.HasValue) q = q.Where(a => a.UsuarioId == usuarioId);
        if (!string.IsNullOrWhiteSpace(entidade)) q = q.Where(a => a.Entidade == entidade);
        if (!string.IsNullOrWhiteSpace(acao)) q = q.Where(a => a.Acao == acao);
        if (!string.IsNullOrWhiteSpace(termo))
            q = q.Where(a => (a.Resumo != null && a.Resumo.Contains(termo))
                || (a.UsuarioNome != null && a.UsuarioNome.Contains(termo))
                || a.Entidade.Contains(termo));

        var total = await q.CountAsync(ct);
        var itens = await q.OrderByDescending(a => a.DataHora)
            .Skip((Math.Max(1, pagina) - 1) * tamanho).Take(tamanho)
            .Select(a => new
            {
                a.Id, a.DataHora, a.UsuarioId, usuario = a.UsuarioNome ?? "(sistema)",
                a.Acao, a.Entidade, a.EntidadeId, a.Resumo, a.Alteracoes, a.Ip
            })
            .ToListAsync(ct);

        return Ok(new { itens, total, pagina, tamanho });
    }

    /// <summary>Valores distintos para os filtros (usuários e entidades que têm log).</summary>
    [HttpGet("filtros")]
    public async Task<IActionResult> Filtros([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var usuarios = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.UsuarioId != null)
            .GroupBy(a => new { a.UsuarioId, a.UsuarioNome })
            .Select(g => new { id = g.Key.UsuarioId, nome = g.Key.UsuarioNome })
            .OrderBy(x => x.nome).ToListAsync(ct);

        var entidades = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId)
            .Select(a => a.Entidade).Distinct().OrderBy(x => x).ToListAsync(ct);

        return Ok(new { usuarios, entidades });
    }
}
