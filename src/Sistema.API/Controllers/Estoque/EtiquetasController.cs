using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

/// <summary>
/// Configuração dos templates de etiqueta por empresa. Permite que o layout
/// personalizado no editor valha para todos os computadores/usuários da loja.
/// </summary>
[ApiController]
[Route("api/etiquetas")]
[Authorize]
public class EtiquetasController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    /// <summary>Obtém a configuração salva de um template (ex.: "ecogranel").</summary>
    [HttpGet("config")]
    public async Task<IActionResult> ObterConfig(
        [FromQuery] Guid empresaId, [FromQuery] string template, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(template))
            return BadRequest(new { mensagem = "Informe o template." });

        var cfg = await db.ConfiguracoesEtiqueta.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Template == template, ct);

        // Sem configuração salva → o cliente usa os padrões do template.
        if (cfg is null) return Ok(new { template, config = (string?)null });

        return Ok(new { template, config = cfg.ConfigJson, atualizadoEm = cfg.AtualizadoEm });
    }

    /// <summary>Salva (cria ou atualiza) a configuração de um template.</summary>
    [HttpPut("config")]
    public async Task<IActionResult> SalvarConfig(
        [FromBody] SalvarConfigEtiquetaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Template))
            return BadRequest(new { mensagem = "Informe o template." });

        var cfg = await db.ConfiguracoesEtiqueta
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId && c.Template == req.Template, ct);

        if (cfg is null)
        {
            cfg = ConfiguracaoEtiqueta.Criar(req.EmpresaId, req.Template, req.Config);
            db.ConfiguracoesEtiqueta.Add(cfg);
        }
        else cfg.Atualizar(req.Config);

        await uow.SalvarAsync(ct);
        return Ok(new { cfg.Id, cfg.Template, cfg.AtualizadoEm });
    }

    /// <summary>Remove a configuração salva — o template volta ao padrão de fábrica.</summary>
    [HttpDelete("config")]
    public async Task<IActionResult> RestaurarPadrao(
        [FromQuery] Guid empresaId, [FromQuery] string template, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesEtiqueta
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Template == template, ct);
        if (cfg is null) return NoContent();

        db.ConfiguracoesEtiqueta.Remove(cfg);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record SalvarConfigEtiquetaRequest(Guid EmpresaId, string Template, string Config);
