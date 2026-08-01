using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>Ponto de Equilíbrio Financeiro.</summary>
[ApiController]
[Route("api/financeiro/ponto-equilibrio")]
[Authorize]
public class PontoEquilibrioController(SistemaDbContext db) : ControllerBase
{
    /// <summary>
    /// Calcula o ponto de equilíbrio usando as CONTAS A PAGAR do mês como custos
    /// fixos (obrigações do período) e a margem de contribuição média das vendas.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Calcular([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, CancellationToken ct)
    {
        var anoRef = ano ?? DateTime.Today.Year;
        var mesRef = mes ?? DateTime.Today.Month;
        var inicio = new DateTime(anoRef, mesRef, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        // Custos fixos = contas a pagar que vencem no mês (exceto canceladas).
        var contasPagar = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == Domain.Financeiro.Entities.TipoLancamento.ContaPagar
                && l.Status != Domain.Financeiro.Entities.StatusLancamento.Cancelado
                && l.DataVencimento >= inicio && l.DataVencimento < fim.AddDays(1))
            .Select(l => new { l.Categoria, l.ValorOriginal })
            .ToListAsync(ct);
        var totalCustosFixos = contasPagar.Sum(c => c.ValorOriginal);

        // Margem de contribuição por item vendido = PrecoUnitario - CustoUnitario
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.DataHora >= inicio && x.v.DataHora < fim.AddDays(1)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                (x, p) => new
                {
                    receita = x.i.PrecoUnitario * x.i.Quantidade,
                    custo = p.CustoUnitario * x.i.Quantidade
                })
            .ToListAsync(ct);

        var receitaTotal = itens.Sum(i => i.receita);
        var custoVariavelTotal = itens.Sum(i => i.custo);
        var margemContribuicao = receitaTotal - custoVariavelTotal;
        var percentualMC = receitaTotal > 0
            ? Math.Round(margemContribuicao / receitaTotal * 100, 2) : 0m;

        // Se o mês não tem vendas (ex.: mês futuro), estima a margem de contribuição
        // pelos últimos 90 dias — assim o PE ainda é projetado com as contas do mês.
        var margemEstimada = false;
        if (percentualMC <= 0)
        {
            var histInicio = DateTime.Today.AddDays(-90);
            var itensHist = await db.ItensVenda.AsNoTracking()
                .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
                .Where(x => x.v.EmpresaId == empresaId
                    && x.v.DataHora >= histInicio && x.v.DataHora < DateTime.Today.AddDays(1)
                    && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
                .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                    (x, p) => new
                    {
                        receita = x.i.PrecoUnitario * x.i.Quantidade,
                        custo = p.CustoUnitario * x.i.Quantidade
                    })
                .ToListAsync(ct);
            var recHist = itensHist.Sum(i => i.receita);
            if (recHist > 0)
            {
                percentualMC = Math.Round((recHist - itensHist.Sum(i => i.custo)) / recHist * 100, 2);
                margemEstimada = true;
            }
        }

        // PE = Custos Fixos / (Margem de Contribuição %)
        var pontoEquilibrio = percentualMC > 0
            ? Math.Round(totalCustosFixos / (percentualMC / 100), 2) : 0m;

        // Progresso do mês atual
        var faturamentoMes = receitaTotal;
        var percentualAtingido = pontoEquilibrio > 0
            ? Math.Round(faturamentoMes / pontoEquilibrio * 100, 2) : 0m;
        var peAtingido = faturamentoMes >= pontoEquilibrio;

        return Ok(new
        {
            periodo = new { ano = anoRef, mes = mesRef },
            totalCustosFixos,
            detalhesCustosFixos = contasPagar.GroupBy(c => c.Categoria ?? "Outros")
                .Select(g => new { categoria = g.Key, total = g.Sum(x => x.ValorOriginal) }),
            receitaTotal,
            custoVariavelTotal,
            margemContribuicao,
            percentualMargemContribuicao = percentualMC,
            margemEstimada,
            pontoEquilibrio,
            faturamentoMes,
            percentualAtingido,
            peAtingido,
            lucroAcimaPE = peAtingido ? Math.Round(faturamentoMes - pontoEquilibrio, 2) : 0m,
            diasParaAtingir = pontoEquilibrio > 0 && faturamentoMes > 0
                ? Math.Round((double)pontoEquilibrio / (double)(faturamentoMes / DateTime.Today.Day), 0) : 0
        });
    }

    /// <summary>Meta de venda POR DIA para cobrir (a) as despesas operacionais e (b) os
    /// financiamentos do mês — cada uma dividida pela margem de contribuição e pelos dias do mês.</summary>
    [HttpGet("metas-diarias")]
    public async Task<IActionResult> MetasDiarias([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, CancellationToken ct)
    {
        var anoRef = ano ?? DateTime.Today.Year;
        var mesRef = mes ?? DateTime.Today.Month;
        var inicio = new DateTime(anoRef, mesRef, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);
        var diasMes = DateTime.DaysInMonth(anoRef, mesRef);

        var contas = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == Domain.Financeiro.Entities.TipoLancamento.ContaPagar
                && l.Status != Domain.Financeiro.Entities.StatusLancamento.Cancelado
                && l.DataVencimento >= inicio && l.DataVencimento < fim.AddDays(1))
            .Select(l => new { l.Categoria, l.ValorOriginal })
            .ToListAsync(ct);

        // Financiamento/capital: empréstimos + imobilizado (móveis/equipamentos financiados).
        // Estoque (Custo CMV) fica de fora das duas metas: já é coberto pela margem da venda.
        static bool EhFinanciamento(string? c) => c is "Financiamentos" or "Imobilizado";
        static bool EhEstoque(string? c) => c is "Custo (CMV)";

        var despesasOperacionais = contas
            .Where(c => !EhFinanciamento(c.Categoria) && !EhEstoque(c.Categoria)).Sum(c => c.ValorOriginal);
        var financiamentos = contas.Where(c => EhFinanciamento(c.Categoria)).Sum(c => c.ValorOriginal);

        var mc = await MargemContribuicaoAsync(empresaId, inicio, fim, ct);

        decimal MetaDia(decimal valorMes) =>
            mc > 0 ? Math.Round(valorMes / (mc / 100m) / diasMes, 2) : 0m;

        return Ok(new
        {
            periodo = new { ano = anoRef, mes = mesRef, diasMes },
            percentualMargemContribuicao = mc,
            despesasOperacionaisMes = despesasOperacionais,
            financiamentosMes = financiamentos,
            metaDiariaOperacional = MetaDia(despesasOperacionais),
            metaDiariaFinanciamentos = MetaDia(financiamentos),
            metaDiariaTotal = MetaDia(despesasOperacionais + financiamentos)
        });
    }

    /// <summary>Margem de contribuição % do mês; se o mês não tem vendas, usa os últimos 90 dias.</summary>
    private async Task<decimal> MargemContribuicaoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct)
    {
        async Task<decimal> Calc(DateTime de, DateTime ate)
        {
            var itens = await db.ItensVenda.AsNoTracking()
                .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
                .Where(x => x.v.EmpresaId == empresaId && x.v.DataHora >= de && x.v.DataHora < ate
                    && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
                .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                    (x, p) => new { receita = x.i.PrecoUnitario * x.i.Quantidade, custo = p.CustoUnitario * x.i.Quantidade })
                .ToListAsync(ct);
            var rec = itens.Sum(i => i.receita);
            return rec > 0 ? Math.Round((rec - itens.Sum(i => i.custo)) / rec * 100, 2) : 0m;
        }

        var mc = await Calc(inicio, fim.AddDays(1));
        if (mc <= 0) mc = await Calc(DateTime.Today.AddDays(-90), DateTime.Today.AddDays(1));
        return mc;
    }
}
