using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/alimentos-taco")]
[Authorize]
public class AlimentosTacoController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Buscar([FromQuery] string? q, [FromQuery] string? grupo,
        [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var query = db.AlimentosTaco.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.Nome.Contains(q));

        if (!string.IsNullOrWhiteSpace(grupo))
            query = query.Where(a => a.GrupoAlimentar == grupo);

        var items = await query
            .OrderBy(a => a.GrupoAlimentar).ThenBy(a => a.Nome)
            .Take(limit)
            .Select(a => new
            {
                a.Id, a.Nome, a.GrupoAlimentar, a.Fonte,
                a.CaloriasKcal, a.Carboidratos, a.Proteinas, a.LipidiosTotais,
                a.FibraAlimentar, a.Sodio, a.Calcio, a.Ferro,
                a.GordurasSaturadas, a.GordurasTrans, a.VitaminaC, a.VitaminaA,
                a.Zinco, a.Potassio, a.VitaminaB6, a.VitaminaB12,
                a.AcidoFolico, a.Magnesio, a.Selenio,
                a.GordurasMono, a.GordurasPoli, a.Colesterol,
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("grupos")]
    public async Task<IActionResult> ListarGrupos(CancellationToken ct)
    {
        var grupos = await db.AlimentosTaco.AsNoTracking()
            .Select(a => a.GrupoAlimentar)
            .Distinct()
            .OrderBy(g => g)
            .ToListAsync(ct);
        return Ok(grupos);
    }
}
