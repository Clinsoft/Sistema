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
                // Não conta como despesa: compra de mercadoria (já entra pelo CMV do que foi
                // vendido) nem capital/imobilizado (móveis, equipamentos são investimento).
                && l.Categoria != "Custo (CMV)" && l.Categoria != "Imobilizado"
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

        // Outras Receitas Operacionais recebidas no período (ex.: ajuste de fechamento,
        // subvenções). Entram no resultado como receita — mas NÃO são venda nem aporte de capital.
        var outrasReceitas = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaReceber
                && l.Categoria == "Outras Receitas Operacionais"
                && l.DataPagamento >= inicio && l.DataPagamento < fim.AddDays(1)
                && (l.Status == StatusLancamento.Pago || l.Status == StatusLancamento.PagoParcialmente))
            .SumAsync(l => l.ValorPago, ct);

        var receitaBruta = receitas + descontosVendas;
        var receitaLiquida = receitas;
        var lucroBruto = receitaLiquida - cmv;
        var margemBruta = receitaLiquida > 0 ? Math.Round(lucroBruto / receitaLiquida * 100, 2) : 0m;
        var resultadoOperacional = lucroBruto - despesasPagas + outrasReceitas;
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
            outrasReceitas,
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
            .Select(l => new { l.Tipo, l.Categoria, l.Descricao, l.ValorOriginal, l.ValorJuros })
            .ToListAsync(ct);

        // Não são receita: Aporte de Capital (injeção do sócio) nem Empréstimo Captado
        // (dinheiro do banco que entrou no caixa — é passivo, não faturamento).
        var receber = lancs.Where(l => l.Tipo == TipoLancamento.ContaReceber
            && l.Categoria != "Aporte de Capital" && l.Categoria != "Empréstimo Captado").ToList();
        // Fora do RESULTADO operacional: Imobilizado (investimento), Custo (CMV) (compra de
        // mercadoria — entra pelo CMV do vendido), Financiamentos (amortização do principal — reduz
        // passivo, não é despesa) e Frete (custo de compra — somado ao CMV abaixo).
        // OBS: os JUROS do financiamento entram, sim (categoria "Despesas Financeiras").
        var pagar = lancs.Where(l => l.Tipo == TipoLancamento.ContaPagar
            && l.Categoria != "Imobilizado" && l.Categoria != "Custo (CMV)"
            && l.Categoria != "Financiamentos" && l.Categoria != "Frete").ToList();

        // Frete de compra entra como CUSTO (junto do CMV), não como despesa operacional.
        var freteCompra = lancs.Where(l => l.Tipo == TipoLancamento.ContaPagar && l.Categoria == "Frete")
            .Sum(l => l.ValorOriginal);

        // Juros embutidos nas parcelas de financiamento: a parcela (uma só, categoria "Financiamentos")
        // é paga inteira; a parte de juro (ValorJuros) é DESPESA e o restante é amortização (fora do resultado).
        var jurosFinanciamento = lancs.Where(l => l.Tipo == TipoLancamento.ContaPagar
            && l.Categoria == "Financiamentos").Sum(l => l.ValorJuros ?? 0);

        // Vendas do PDV pagas na hora (não-crediário): são RECEITA, mas não viram conta a receber.
        var vendasPdv = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && v.DataHora >= inicio && v.DataHora < fimExcl
                && !v.Pagamentos.Any(p => p.Forma == Domain.Vendas.Entities.FormaPagamento.Crediario))
            .SumAsync(v => v.Total, ct);

        // CMV — custo do que foi VENDIDO no período (qtd × custo do produto), não a compra de estoque.
        var cmv = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && x.v.DataHora >= inicio && x.v.DataHora < fimExcl)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id, (x, p) => x.i.Quantidade * p.CustoUnitario)
            .SumAsync(v => (decimal?)v ?? 0, ct);
        cmv += freteCompra;   // custo do vendido + frete de compra

        // Subcategorias dos recebimentos: Vendas (PDV) + os lançamentos de conta a receber.
        var subRecebimentos = new List<object>();
        if (vendasPdv > 0) subRecebimentos.Add(new { nome = "Vendas (PDV)", total = vendasPdv });
        subRecebimentos.AddRange(receber
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Categoria) ? "Recebimentos" : l.Categoria!)
            .Select(g => new { nome = g.Key, total = g.Sum(x => x.ValorOriginal) })
            .OrderByDescending(x => x.total)
            .Cast<object>());

        // Despesas por macro-categoria; desconhecidas caem em "Despesas Variáveis"
        static string Grupo(string? cat) => cat switch
        {
            "Despesas Administrativas" => "Despesas Administrativas",
            "Despesas Operacionais" => "Despesas Operacionais",
            "Pessoas" => "Pessoas",
            "Impostos" => "Impostos",
            "Despesas Financeiras" => "Despesas Financeiras",   // juros de empréstimo/financiamento
            _ => "Despesas Variáveis"
        };

        List<object> SubPorDescricao(string grupo) => pagar
            .Where(l => Grupo(l.Categoria) == grupo)
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Descricao) ? "Outros" : l.Descricao)
            .Select(g => (object)new { nome = g.Key, total = g.Sum(x => x.ValorOriginal) })
            .OrderByDescending(x => ((dynamic)x).total).ToList();

        decimal TotalGrupo(string grupo) => pagar.Where(l => Grupo(l.Categoria) == grupo).Sum(l => l.ValorOriginal);

        var recebimentos = receber.Sum(l => l.ValorOriginal) + vendasPdv;
        var despesasAdministrativas = TotalGrupo("Despesas Administrativas");
        var despesasOperacionais = TotalGrupo("Despesas Operacionais");
        var despesasVariaveis = TotalGrupo("Despesas Variáveis");
        var pessoas = TotalGrupo("Pessoas");
        var impostos = TotalGrupo("Impostos");
        var despesasFinanceiras = TotalGrupo("Despesas Financeiras") + jurosFinanciamento;
        var resultado = recebimentos - cmv - despesasAdministrativas - despesasOperacionais
                      - despesasVariaveis - pessoas - impostos - despesasFinanceiras;
        var margemLiquida = recebimentos > 0 ? Math.Round(resultado / recebimentos * 100, 1) : 0m;

        return new
        {
            recebimentos, cmv, despesasAdministrativas, despesasOperacionais,
            despesasVariaveis, pessoas, impostos, despesasFinanceiras,
            resultado, margemLiquida,
            subcategorias = new
            {
                recebimentos = subRecebimentos,
                despesasAdministrativas = SubPorDescricao("Despesas Administrativas"),
                despesasOperacionais = SubPorDescricao("Despesas Operacionais"),
                despesasVariaveis = SubPorDescricao("Despesas Variáveis"),
                pessoas = SubPorDescricao("Pessoas"),
                impostos = SubPorDescricao("Impostos"),
                despesasFinanceiras = (jurosFinanciamento > 0
                        ? new List<object> { new { nome = "Juros de financiamento", total = jurosFinanciamento } }
                        : new List<object>())
                    .Concat(SubPorDescricao("Despesas Financeiras")).ToList()
            }
        };
    }
}
