using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Crediario;

[ApiController]
[Route("api/relatorios/crediario")]
[Authorize]
public class RelatorioCrediarioController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Contas a receber do crediário — parcelas em aberto.</summary>
    [HttpGet("contas-receber")]
    public async Task<IActionResult> ContasReceber([FromQuery] Guid empresaId,
        [FromQuery] DateTime? vencimentoAte, CancellationToken ct)
    {
        var query = db.ParcelasCrediario.AsNoTracking()
            .Where(p => p.Status == Domain.Crediario.Entities.StatusParcela.EmAberto);

        if (vencimentoAte.HasValue)
            query = query.Where(p => p.DataVencimento <= vencimentoAte);

        var parcelas = await query
            .Join(db.Crediarios, p => p.CrediarioId, c => c.Id,
                (p, c) => new { p, c })
            .Where(x => x.c.EmpresaId == empresaId)
            .Join(db.Clientes, x => x.c.ClienteId, cl => cl.Id,
                (x, cl) => new
                {
                    x.p.Id, x.p.Numero, x.p.Valor, x.p.DataVencimento,
                    x.p.Status, CrediarioNumero = x.c.Numero,
                    ClienteId = cl.Id, ClienteNome = cl.Nome, cl.Telefone,
                    DiasAtraso = x.p.DataVencimento < DateTime.Today
                        ? (int)(DateTime.Today - x.p.DataVencimento).TotalDays : 0,
                    ValorAtualizado = x.p.Valor // domínio calcula via p.ValorAtualizado()
                })
            .OrderBy(x => x.DataVencimento)
            .ToListAsync(ct);

        return Ok(new
        {
            parcelas,
            totalEmAberto = parcelas.Sum(p => p.Valor),
            totalVencidas = parcelas.Where(p => p.DiasAtraso > 0).Sum(p => p.Valor),
            qtdInadimplentes = parcelas.Count(p => p.DiasAtraso > 0)
        });
    }

    /// <summary>Inadimplentes — clientes com parcelas vencidas há mais de X dias.</summary>
    [HttpGet("inadimplentes")]
    public async Task<IActionResult> Inadimplentes([FromQuery] Guid empresaId,
        [FromQuery] int diasAtraso = 1, CancellationToken ct = default)
    {
        var limiteData = DateTime.Today.AddDays(-diasAtraso);

        var inadimplentes = await db.ParcelasCrediario.AsNoTracking()
            .Where(p => p.Status == Domain.Crediario.Entities.StatusParcela.EmAberto
                && p.DataVencimento < limiteData)
            .Join(db.Crediarios, p => p.CrediarioId, c => c.Id, (p, c) => new { p, c })
            .Where(x => x.c.EmpresaId == empresaId)
            .Join(db.Clientes, x => x.c.ClienteId, cl => cl.Id,
                (x, cl) => new
                {
                    ClienteId = cl.Id, ClienteNome = cl.Nome, cl.Telefone, cl.Celular,
                    x.p.Id, x.p.Numero, x.p.Valor, x.p.DataVencimento,
                    DiasAtraso = (int)(DateTime.Today - x.p.DataVencimento).TotalDays
                })
            .GroupBy(x => new { x.ClienteId, x.ClienteNome, x.Telefone, x.Celular })
            .Select(g => new
            {
                g.Key.ClienteId, g.Key.ClienteNome, g.Key.Telefone, g.Key.Celular,
                qtdParcelasVencidas = g.Count(),
                totalVencido = g.Sum(x => x.Valor),
                maiorAtraso = g.Max(x => x.DiasAtraso)
            })
            .OrderByDescending(x => x.totalVencido)
            .ToListAsync(ct);

        return Ok(inadimplentes);
    }
}
