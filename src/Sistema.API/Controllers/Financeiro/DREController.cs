using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>Demonstrativo de Resultados do Exercício (DRE).</summary>
[ApiController]
[Route("api/financeiro/dre")]
[Authorize(Roles = "Administrador,Financeiro,Contador")]
public class DREController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        // Receitas operacionais — vendas finalizadas no período
        var receitas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora <= fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .SumAsync(v => v.Total, ct);

        var descontosVendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora <= fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .SumAsync(v => v.TotalDesconto, ct);

        // CMV — Custo da mercadoria vendida (custo do produto × qty vendida)
        var cmv = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.DataHora >= inicio && x.v.DataHora <= fim.AddDays(1)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                (x, p) => x.i.Quantidade * p.CustoUnitario)
            .SumAsync(v => (decimal?)v ?? 0, ct);

        // Despesas pagas no período (contas a pagar baixadas)
        var despesasPagas = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.DataPagamento >= inicio && l.DataPagamento <= fim.AddDays(1)
                && (l.Status == StatusLancamento.Pago || l.Status == StatusLancamento.PagoParcialmente))
            .SumAsync(l => l.ValorPago, ct);

        // Despesas por categoria
        var despesasPorCategoria = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.DataPagamento >= inicio && l.DataPagamento <= fim.AddDays(1)
                && l.Status == StatusLancamento.Pago)
            .Join(db.CategoriasFinanceiras, l => l.CategoriaId, c => c.Id,
                (l, c) => new { c.Nome, l.ValorPago })
            .GroupBy(x => x.Nome)
            .Select(g => new { categoria = g.Key, total = g.Sum(x => x.ValorPago) })
            .ToListAsync(ct);

        var receitaBruta = receitas + descontosVendas;
        var receitaLiquida = receitas;
        var lucroBruto = receitaLiquida - cmv;
        var margemBruta = receitaLiquida > 0 ? Math.Round(lucroBruto / receitaLiquida * 100, 2) : 0m;
        var resultadoOperacional = lucroBruto - despesasPagas;
        var margemOperacional = receitaLiquida > 0 ? Math.Round(resultadoOperacional / receitaLiquida * 100, 2) : 0m;

        return Ok(new
        {
            periodo = new { ano, mes, inicio, fim },
            receitaBruta,
            descontos = descontosVendas,
            receitaLiquida,
            cmv,
            lucroBruto,
            margemBruta,
            despesasOperacionais = despesasPagas,
            despesasPorCategoria,
            resultadoOperacional,
            margemOperacional
        });
    }

    /// <summary>DRE anual — um mês por coluna.</summary>
    [HttpGet("anual")]
    public async Task<IActionResult> Anual([FromQuery] Guid empresaId,
        [FromQuery] int ano, CancellationToken ct)
    {
        var meses = new List<object>();

        for (int mes = 1; mes <= 12; mes++)
        {
            var inicio = new DateTime(ano, mes, 1);
            var fim = inicio.AddMonths(1).AddDays(-1);

            var receitas = await db.Vendas.AsNoTracking()
                .Where(v => v.EmpresaId == empresaId
                    && v.DataHora >= inicio && v.DataHora < fim.AddDays(1)
                    && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
                .SumAsync(v => (decimal?)v.Total ?? 0, ct);

            var despesas = await db.LancamentosFinanceiros.AsNoTracking()
                .Where(l => l.EmpresaId == empresaId
                    && l.Tipo == TipoLancamento.ContaPagar
                    && l.DataPagamento >= inicio && l.DataPagamento < fim.AddDays(1)
                    && l.Status == StatusLancamento.Pago)
                .SumAsync(l => (decimal?)l.ValorPago ?? 0, ct);

            meses.Add(new
            {
                mes, nomeMes = inicio.ToString("MMM", new System.Globalization.CultureInfo("pt-BR")),
                receitas, despesas, resultado = receitas - despesas
            });
        }

        return Ok(new
        {
            ano,
            meses,
            totalReceitas = meses.Sum(m => (decimal)((dynamic)m).receitas),
            totalDespesas = meses.Sum(m => (decimal)((dynamic)m).despesas),
            resultado = meses.Sum(m => (decimal)((dynamic)m).resultado)
        });
    }
}
