using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

[ApiController]
[Route("api/relatorios/clientes")]
[Authorize]
public class RelatoriosClientesController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Clientes aniversariantes do mês.</summary>
    [HttpGet("aniversariantes")]
    public async Task<IActionResult> Aniversariantes([FromQuery] Guid empresaId,
        [FromQuery] int? mes, CancellationToken ct)
    {
        var mesRef = mes ?? DateTime.Now.Month;
        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo
                && c.DataNascimento.HasValue
                && c.DataNascimento.Value.Month == mesRef)
            .OrderBy(c => c.DataNascimento!.Value.Day)
            .Select(c => new
            {
                c.Id, c.Nome, c.CpfCnpj, c.Telefone, c.Celular, c.Email,
                c.DataNascimento, c.PontosFidelidade
            })
            .ToListAsync(ct);

        return Ok(clientes);
    }

    /// <summary>Clientes por produto comprado.</summary>
    [HttpGet("por-produto/{produtoId:guid}")]
    public async Task<IActionResult> ClientesPorProduto(Guid produtoId,
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var clienteIds = await db.ItensVenda.AsNoTracking()
            .Where(i => i.ProdutoId == produtoId)
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { v.ClienteId, v.DataHora, v.EmpresaId, v.Status })
            .Where(x => x.EmpresaId == empresaId
                && x.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && x.DataHora >= inicio && x.DataHora < fim.AddDays(1)
                && x.ClienteId.HasValue)
            .Select(x => x.ClienteId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => clienteIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Nome, c.CpfCnpj, c.Telefone, c.Email })
            .ToListAsync(ct);

        return Ok(clientes);
    }

    /// <summary>Ranking de clientes por valor comprado (Curva ABC de clientes).</summary>
    [HttpGet("ranking")]
    public async Task<IActionResult> Ranking([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var ranking = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && v.DataHora >= inicio && v.DataHora < fim.AddDays(1)
                && v.ClienteId.HasValue)
            .GroupBy(v => v.ClienteId!.Value)
            .Select(g => new
            {
                clienteId = g.Key,
                qtdCompras = g.Count(),
                totalComprado = g.Sum(v => v.Total),
                ticketMedio = g.Average(v => v.Total),
                ultimaCompra = g.Max(v => v.DataHora)
            })
            .OrderByDescending(x => x.totalComprado)
            .ToListAsync(ct);

        // Enriquecer com nome do cliente
        var ids = ranking.Select(r => r.clienteId).ToList();
        var nomes = await db.Clientes.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, ct);

        var total = ranking.Sum(r => r.totalComprado);
        var acumulado = 0m;

        return Ok(ranking.Select(r =>
        {
            acumulado += r.totalComprado;
            var pct = total > 0 ? acumulado / total * 100 : 0;
            return new
            {
                r.clienteId,
                nome = nomes.GetValueOrDefault(r.clienteId, ""),
                r.qtdCompras, r.totalComprado, r.ticketMedio, r.ultimaCompra,
                curva = pct <= 80 ? "A" : pct <= 95 ? "B" : "C"
            };
        }));
    }
}
