using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

[ApiController]
[Route("api/relatorios/pagamentos")]
[Authorize]
public class RelatoriosPagamentosController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Vendas por meio de pagamento no período.</summary>
    [HttpGet("por-forma")]
    public async Task<IActionResult> PorForma([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var pagamentos = await db.PagamentosVenda.AsNoTracking()
            .Join(db.Vendas, p => p.VendaId, v => v.Id,
                (p, v) => new { p, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.DataHora >= inicio && x.v.DataHora < fim.AddDays(1)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .GroupBy(x => x.p.Forma)
            .Select(g => new
            {
                forma = g.Key.ToString(),
                qtd = g.Count(),
                total = g.Sum(x => x.p.Valor)
            })
            .OrderByDescending(x => x.total)
            .ToListAsync(ct);

        var totalGeral = pagamentos.Sum(p => p.total);

        return Ok(new
        {
            periodo = new { inicio, fim },
            pagamentos = pagamentos.Select(p => new
            {
                p.forma, p.qtd, p.total,
                percentual = totalGeral > 0 ? Math.Round(p.total / totalGeral * 100, 2) : 0m
            }),
            totalGeral
        });
    }

    /// <summary>Resumo de fechamento dos caixas no período.</summary>
    [HttpGet("fechamentos")]
    public async Task<IActionResult> Fechamentos([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var sessoes = await db.PDVSessoes.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId
                && s.Abertura >= inicio
                && s.Fechamento.HasValue && s.Fechamento < fim.AddDays(1))
            .OrderByDescending(s => s.Abertura)
            .Select(s => new
            {
                s.Id, s.Abertura, s.Fechamento,
                s.SaldoAbertura, s.SaldoFechamento,
                s.TotalVendas, s.TotalSuprimentos, s.TotalSangrias,
                saldoEsperado = s.SaldoAbertura + s.TotalVendas + s.TotalSuprimentos - s.TotalSangrias,
                diferenca = s.SaldoFechamento - (s.SaldoAbertura + s.TotalVendas + s.TotalSuprimentos - s.TotalSangrias)
            })
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            qtdSessoes = sessoes.Count,
            sessoes,
            totalDiferenca = sessoes.Sum(s => s.diferenca)
        });
    }
}
