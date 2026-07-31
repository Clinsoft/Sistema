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
                && v.DataHora >= inicio && v.DataHora < fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .SumAsync(v => v.Total, ct);

        var descontosVendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora < fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .SumAsync(v => v.TotalDesconto, ct);

        // CMV — Custo da mercadoria vendida (custo do produto × qty vendida)
        var cmv = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.DataHora >= inicio && x.v.DataHora < fim.AddDays(1)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                (x, p) => x.i.Quantidade * p.CustoUnitario)
            .SumAsync(v => (decimal?)v ?? 0, ct);

        // Despesas pagas no período (contas a pagar baixadas)
        var despesasPagas = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.DataPagamento >= inicio && l.DataPagamento < fim.AddDays(1)
                && (l.Status == StatusLancamento.Pago || l.Status == StatusLancamento.PagoParcialmente))
            .SumAsync(l => l.ValorPago, ct);

        // Despesas por categoria
        var despesasPorCategoria = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.DataPagamento >= inicio && l.DataPagamento < fim.AddDays(1)
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

    /// <summary>DRE gerencial mensal por categoria (Recebimentos, Despesas Administrativas/
    /// Operacionais/Variáveis, Pessoas, Impostos).</summary>
    [HttpGet("mensal")]
    public async Task<IActionResult> Mensal([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);
        return Ok(await MontarDreCategorizado(empresaId, inicio, fim, ct));
    }

    /// <summary>DRE gerencial anual por categoria (exercício inteiro).</summary>
    [HttpGet("anual")]
    public async Task<IActionResult> Anual([FromQuery] Guid empresaId,
        [FromQuery] int ano, CancellationToken ct)
    {
        var inicio = new DateTime(ano, 1, 1);
        var fim = new DateTime(ano, 12, 31);
        return Ok(await MontarDreCategorizado(empresaId, inicio, fim, ct));
    }

    /// <summary>
    /// Monta o DRE gerencial por categoria (competência = vencimento no período),
    /// a partir dos lançamentos financeiros não cancelados.
    /// </summary>
    private async Task<object> MontarDreCategorizado(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct)
    {
        var fimExcl = fim.AddDays(1);
        var lancs = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Status != StatusLancamento.Cancelado
                && l.DataVencimento >= inicio && l.DataVencimento < fimExcl)
            .Select(l => new { l.Tipo, l.Categoria, l.Descricao, l.ValorOriginal })
            .ToListAsync(ct);

        var receber = lancs.Where(l => l.Tipo == TipoLancamento.ContaReceber).ToList();
        var pagar = lancs.Where(l => l.Tipo == TipoLancamento.ContaPagar).ToList();

        // Subcategorias dos recebimentos agrupadas pela categoria (Vendas, Serviços…)
        var subRecebimentos = receber
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Categoria) ? "Recebimentos" : l.Categoria!)
            .Select(g => new { nome = g.Key, total = g.Sum(x => x.ValorOriginal) })
            .OrderByDescending(x => x.total).ToList();

        // Despesas por macro-categoria; desconhecidas caem em "Despesas Variáveis"
        static string Grupo(string? cat) => cat switch
        {
            "Despesas Administrativas" => "Despesas Administrativas",
            "Despesas Operacionais" => "Despesas Operacionais",
            "Pessoas" => "Pessoas",
            "Impostos" => "Impostos",
            _ => "Despesas Variáveis"
        };

        List<object> SubPorDescricao(string grupo) => pagar
            .Where(l => Grupo(l.Categoria) == grupo)
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Descricao) ? "Outros" : l.Descricao)
            .Select(g => (object)new { nome = g.Key, total = g.Sum(x => x.ValorOriginal) })
            .OrderByDescending(x => ((dynamic)x).total).ToList();

        decimal TotalGrupo(string grupo) => pagar.Where(l => Grupo(l.Categoria) == grupo).Sum(l => l.ValorOriginal);

        var recebimentos = receber.Sum(l => l.ValorOriginal);
        var despesasAdministrativas = TotalGrupo("Despesas Administrativas");
        var despesasOperacionais = TotalGrupo("Despesas Operacionais");
        var despesasVariaveis = TotalGrupo("Despesas Variáveis");
        var pessoas = TotalGrupo("Pessoas");
        var impostos = TotalGrupo("Impostos");
        var resultado = recebimentos - despesasAdministrativas - despesasOperacionais
                      - despesasVariaveis - pessoas - impostos;
        var margemLiquida = recebimentos > 0 ? Math.Round(resultado / recebimentos * 100, 1) : 0m;

        return new
        {
            recebimentos, despesasAdministrativas, despesasOperacionais,
            despesasVariaveis, pessoas, impostos,
            resultado, margemLiquida,
            subcategorias = new
            {
                recebimentos = subRecebimentos,
                despesasAdministrativas = SubPorDescricao("Despesas Administrativas"),
                despesasOperacionais = SubPorDescricao("Despesas Operacionais"),
                despesasVariaveis = SubPorDescricao("Despesas Variáveis"),
                pessoas = SubPorDescricao("Pessoas"),
                impostos = SubPorDescricao("Impostos")
            }
        };
    }
}
