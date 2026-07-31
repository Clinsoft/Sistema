using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

[ApiController]
[Route("api/relatorios/vendas")]
[Authorize]
public class RelatoriosVendasComplementoController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Ticket médio diário no período.</summary>
    [HttpGet("ticket-medio")]
    public async Task<IActionResult> TicketMedio([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora < fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Select(v => new { v.DataHora, v.Total })
            .ToListAsync(ct);

        var porDia = vendas
            .GroupBy(v => v.DataHora.Date)
            .Select(g => new
            {
                data = g.Key,
                qtdVendas = g.Count(),
                totalFaturado = g.Sum(v => v.Total),
                ticketMedio = g.Any() ? Math.Round(g.Sum(v => v.Total) / g.Count(), 2) : 0m
            })
            .OrderBy(d => d.data)
            .ToList();

        return Ok(new
        {
            periodo = new { inicio, fim },
            totalVendas = vendas.Count,
            totalFaturado = vendas.Sum(v => v.Total),
            ticketMedioGeral = vendas.Any() ? Math.Round(vendas.Sum(v => v.Total) / vendas.Count, 2) : 0m,
            porDia
        });
    }

    /// <summary>Faturamento por período (mensal/semanal).</summary>
    [HttpGet("faturamento")]
    public async Task<IActionResult> Faturamento([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] string agrupamento = "diario", CancellationToken ct = default)
    {
        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora < fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Select(v => new { v.DataHora, v.Total, Desconto = v.TotalDesconto })
            .ToListAsync(ct);

        var resultado = agrupamento.ToLower() switch
        {
            "mensal" => vendas
                .GroupBy(v => new { v.DataHora.Year, v.DataHora.Month })
                .Select(g => new
                {
                    periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                    qtd = g.Count(),
                    bruto = g.Sum(v => v.Total + v.Desconto),
                    descontos = g.Sum(v => v.Desconto),
                    liquido = g.Sum(v => v.Total)
                })
                .OrderBy(x => x.periodo)
                .Cast<object>().ToList(),
            "semanal" => vendas
                .GroupBy(v => System.Globalization.ISOWeek.GetWeekOfYear(v.DataHora))
                .Select(g => new
                {
                    semana = g.Key,
                    qtd = g.Count(),
                    bruto = g.Sum(v => v.Total + v.Desconto),
                    descontos = g.Sum(v => v.Desconto),
                    liquido = g.Sum(v => v.Total)
                })
                .OrderBy(x => x.semana)
                .Cast<object>().ToList(),
            _ => vendas  // diario
                .GroupBy(v => v.DataHora.Date)
                .Select(g => new
                {
                    data = g.Key.ToString("yyyy-MM-dd"),
                    qtd = g.Count(),
                    bruto = g.Sum(v => v.Total + v.Desconto),
                    descontos = g.Sum(v => v.Desconto),
                    liquido = g.Sum(v => v.Total)
                })
                .OrderBy(x => x.data)
                .Cast<object>().ToList()
        };

        return Ok(new
        {
            periodo = new { inicio, fim }, agrupamento,
            totalGeral = vendas.Sum(v => v.Total),
            totalDescontos = vendas.Sum(v => v.Desconto),
            resultado
        });
    }

    /// <summary>Alterações de preço dos produtos.</summary>
    [HttpGet("alteracoes-preco")]
    public async Task<IActionResult> AlteracoesPreco([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        // Preço é registrado em cada ItemVenda — comparamos com o preço atual do produto
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.DataHora >= inicio && x.v.DataHora < fim.AddDays(1))
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                (x, p) => new
                {
                    x.i.ProdutoId, ProdutoNome = p.Descricao,
                    precoVendaAtual = p.PrecoVenda,
                    precoVendido = x.i.PrecoUnitario,
                    x.v.DataHora,
                    diferenca = x.i.PrecoUnitario - p.PrecoVenda
                })
            .Where(x => Math.Abs(x.diferenca) > 0.01m) // só onde houve diferença
            .OrderByDescending(x => x.DataHora)
            .ToListAsync(ct);

        return Ok(new { total = itens.Count, alteracoes = itens });
    }
}
