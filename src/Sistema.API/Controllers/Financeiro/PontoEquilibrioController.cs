using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>Ponto de Equilíbrio Financeiro.</summary>
[ApiController]
[Route("api/financeiro/ponto-equilibrio")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
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
        // Exclui a compra de estoque (CMV): é custo VARIÁVEL, já coberto pela margem de
        // contribuição — somá-la aos custos fixos inflaria o ponto de equilíbrio.
        var contasPagar = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == Domain.Financeiro.Entities.TipoLancamento.ContaPagar
                && l.Status != Domain.Financeiro.Entities.StatusLancamento.Cancelado
                && l.Categoria != "Custo (CMV)"
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

        // PE OPERACIONAL = tira a DÍVIDA/INVESTIMENTO (financiamento + imobilizado financiado)
        // dos custos fixos — é o quanto a loja precisa vender só para manter a operação, sem a
        // dívida. (Imobilizado parcelado é dívida de investimento, não custo recorrente de operar;
        // continua no PE TOTAL, que é o que garante que a loja se paga por completo.)
        var financiamentoMes = contasPagar
            .Where(c => c.Categoria == "Financiamentos" || c.Categoria == "Imobilizado")
            .Sum(c => c.ValorOriginal);
        var custosOperacionais = totalCustosFixos - financiamentoMes;
        var pontoEquilibrioOperacional = percentualMC > 0
            ? Math.Round(custosOperacionais / (percentualMC / 100), 2) : 0m;

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
            financiamentoMes,
            custosOperacionais,
            pontoEquilibrioOperacional,
            faturamentoMes,
            percentualAtingido,
            peAtingido,
            lucroAcimaPE = peAtingido ? Math.Round(faturamentoMes - pontoEquilibrio, 2) : 0m,
            diasParaAtingir = pontoEquilibrio > 0 && faturamentoMes > 0
                ? Math.Round((double)pontoEquilibrio / (double)(faturamentoMes / DateTime.Today.Day), 0) : 0
        });
    }

    /// <summary>
    /// Ponto de equilíbrio POR LOJA. Mesmos custos fixos do cálculo consolidado
    /// (todas as contas a pagar do mês), mas distribuídos como no DRE por loja:
    /// despesas DIRETAS (lançamento com a loja marcada) + rateio das despesas SEM loja
    /// pela participação de cada loja no faturamento. A margem de contribuição é a da
    /// PRÓPRIA loja (vendas da loja no mês; se não houver, estima pelos últimos 90 dias).
    /// </summary>
    [HttpGet("por-lojas")]
    public async Task<IActionResult> PorLojas([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, CancellationToken ct)
    {
        var anoRef = ano ?? DateTime.Today.Year;
        var mesRef = mes ?? DateTime.Today.Month;
        var inicio = new DateTime(anoRef, mesRef, 1);
        var fimExcl = inicio.AddMonths(1);
        var vFinalizada = Domain.Vendas.Entities.StatusVenda.Finalizada;

        var lojas = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId)
            .Select(l => new { l.Id, l.Nome }).ToListAsync(ct);

        // Receita do mês por loja (vendas finalizadas).
        var receitaGrp = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == vFinalizada
                && v.DataHora >= inicio && v.DataHora < fimExcl)
            .GroupBy(v => v.LocalEstoqueId)
            .Select(g => new { Loja = g.Key, Receita = g.Sum(v => v.Total) })
            .ToListAsync(ct);
        var receitaLoja = receitaGrp.ToDictionary(x => x.Loja, x => x.Receita);

        // CMV (custo variável) do mês por loja = custo do produto × qtd vendida.
        var cmvGrp = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId && x.v.Status == vFinalizada
                && x.v.DataHora >= inicio && x.v.DataHora < fimExcl)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                (x, p) => new { x.v.LocalEstoqueId, Custo = x.i.Quantidade * p.CustoUnitario })
            .GroupBy(x => x.LocalEstoqueId)
            .Select(g => new { Loja = g.Key, Custo = g.Sum(x => x.Custo) })
            .ToListAsync(ct);
        var cmvLoja = cmvGrp.ToDictionary(x => x.Loja, x => x.Custo);

        // Margem de contribuição % dos últimos 90 dias por loja (fallback p/ loja sem venda no mês).
        var de90 = DateTime.Today.AddDays(-90);
        var hist90 = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId && x.v.Status == vFinalizada
                && x.v.DataHora >= de90 && x.v.DataHora < DateTime.Today.AddDays(1))
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                (x, p) => new { x.v.LocalEstoqueId,
                    rec = x.i.PrecoUnitario * x.i.Quantidade, cus = p.CustoUnitario * x.i.Quantidade })
            .GroupBy(x => x.LocalEstoqueId)
            .Select(g => new { Loja = g.Key, Rec = g.Sum(x => x.rec), Cus = g.Sum(x => x.cus) })
            .ToListAsync(ct);
        var mc90Loja = hist90.ToDictionary(x => x.Loja,
            x => x.Rec > 0 ? Math.Round((x.Rec - x.Cus) / x.Rec * 100, 2) : 0m);

        // Custos fixos = TODAS as contas a pagar do mês (igual ao consolidado), separando:
        //  • DIRETAS   = lançamento com loja marcada (ex.: salário já leva a loja do colaborador);
        //  • FOLHA     = folha SEM loja (FGTS/INSS/provisões E pró-labore — DocumentoOrigem "FOLHA yyyy-MM")
        //                → rateada por Nº DE FUNCIONÁRIOS de cada loja;
        //  • DEMAIS    = os outros compartilhados (financiamento, contador...) → por faturamento.
        var contas = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == Domain.Financeiro.Entities.TipoLancamento.ContaPagar
                && l.Status != Domain.Financeiro.Entities.StatusLancamento.Cancelado
                && l.Categoria != "Custo (CMV)"   // compra de estoque é variável (já na margem)
                && l.DataVencimento >= inicio && l.DataVencimento < fimExcl)
            .Select(l => new { l.ValorOriginal, l.LocalEstoqueId, l.DocumentoOrigem, l.Categoria })
            .ToListAsync(ct);

        var diretaLoja = contas.Where(c => c.LocalEstoqueId != null)
            .GroupBy(c => c.LocalEstoqueId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ValorOriginal));

        // Dívida/investimento (financiamento + imobilizado financiado) — p/ o PE OPERACIONAL (sem dívida).
        bool EhDivida(string? c) => c == "Financiamentos" || c == "Imobilizado";
        var financDiretaLoja = contas.Where(c => c.LocalEstoqueId != null && EhDivida(c.Categoria))
            .GroupBy(c => c.LocalEstoqueId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ValorOriginal));
        var financSemLoja = contas.Where(c => c.LocalEstoqueId == null && EhDivida(c.Categoria))
            .Sum(c => c.ValorOriginal);

        bool EhFolha(string? doc) => doc != null && doc.StartsWith("FOLHA ");
        var custoFolha = contas.Where(c => c.LocalEstoqueId == null
            && EhFolha(c.DocumentoOrigem)).Sum(c => c.ValorOriginal);
        var custoDemaisComp = contas.Where(c => c.LocalEstoqueId == null
            && !EhFolha(c.DocumentoOrigem)).Sum(c => c.ValorOriginal);

        // Nº de funcionários CLT por loja (fonte: usuários com salário, excluindo sócios).
        var funcs = await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Ativo
                && u.Salario != null && u.LocalEstoqueId != null)
            .Select(u => new { u.Cargo, u.LocalEstoqueId })
            .ToListAsync(ct);
        var headPorLoja = funcs.Where(f => !EhSocio(f.Cargo))
            .GroupBy(f => f.LocalEstoqueId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var headTotal = headPorLoja.Values.Sum();

        var receitaTotal = receitaLoja.Values.Sum();
        var nLojas = Math.Max(lojas.Count, 1);

        var resultado = lojas.Select(loja =>
        {
            var receita = receitaLoja.TryGetValue(loja.Id, out var r) ? r : 0m;
            var custoVar = cmvLoja.TryGetValue(loja.Id, out var c) ? c : 0m;
            var margem = receita - custoVar;
            var percMC = receita > 0 ? Math.Round(margem / receita * 100, 2) : 0m;

            // Sem vendas no mês → estima a margem pelos últimos 90 dias da loja.
            var estimada = false;
            if (percMC <= 0 && mc90Loja.TryGetValue(loja.Id, out var mc90) && mc90 > 0)
            {
                percMC = mc90;
                estimada = true;
            }

            // Rateio: FOLHA pela participação de funcionários; DEMAIS pelo faturamento.
            // Se faltar a base (sem funcionário / sem faturamento), cai no outro critério
            // (e, não havendo nenhum, divide igualmente entre as lojas).
            var shareFat = receitaTotal > 0 ? receita / receitaTotal : 1m / nLojas;
            var shareHead = headTotal > 0
                ? (headPorLoja.TryGetValue(loja.Id, out var h) ? (decimal)h / headTotal : 0m)
                : shareFat;
            var direta = diretaLoja.TryGetValue(loja.Id, out var d) ? d : 0m;
            var rateada = custoFolha * shareHead + custoDemaisComp * shareFat;
            var custosFixos = Math.Round(direta + rateada, 2);

            // Financiamento da loja = direto (com loja) + rateio do sem-loja pelo faturamento.
            var financLoja = Math.Round(
                (financDiretaLoja.TryGetValue(loja.Id, out var fd) ? fd : 0m) + financSemLoja * shareFat, 2);
            var custosOperacionais = Math.Round(custosFixos - financLoja, 2);

            var pe = percMC > 0 ? Math.Round(custosFixos / (percMC / 100m), 2) : 0m;
            var peOperacional = percMC > 0 ? Math.Round(custosOperacionais / (percMC / 100m), 2) : 0m;
            var percAtingido = pe > 0 ? Math.Round(receita / pe * 100, 2) : 0m;
            var atingido = pe > 0 && receita >= pe;

            return new
            {
                lojaId = loja.Id,
                loja = loja.Nome,
                faturamentoMes = Math.Round(receita, 2),
                custoVariavel = Math.Round(custoVar, 2),
                margemContribuicao = Math.Round(margem, 2),
                percentualMargemContribuicao = percMC,
                margemEstimada = estimada,
                custosFixos,
                custosFixosDiretos = Math.Round(direta, 2),
                custosFixosRateados = Math.Round(rateada, 2),
                financiamentoMes = financLoja,
                custosOperacionais,
                funcionarios = headPorLoja.TryGetValue(loja.Id, out var hc) ? hc : 0,
                participacaoReceita = Math.Round(shareFat * 100, 1),
                pontoEquilibrio = pe,
                pontoEquilibrioOperacional = peOperacional,
                percentualAtingido = percAtingido,
                peAtingido = atingido,
                lucroAcimaPE = atingido ? Math.Round(receita - pe, 2) : 0m,
                faltaAtingir = !atingido && pe > 0 ? Math.Round(pe - receita, 2) : 0m,
            };
        }).OrderByDescending(x => x.faturamentoMes).ToList();

        return Ok(new
        {
            periodo = new { ano = anoRef, mes = mesRef },
            totalCustosFixos = Math.Round(contas.Sum(c => c.ValorOriginal), 2),
            custoCompartilhado = Math.Round(custoFolha + custoDemaisComp, 2),
            custoFolhaRateada = Math.Round(custoFolha, 2),
            custoDemaisRateado = Math.Round(custoDemaisComp, 2),
            totalFuncionarios = headTotal,
            lojas = resultado,
            totais = new
            {
                faturamentoMes = Math.Round(resultado.Sum(x => x.faturamentoMes), 2),
                custosFixos = Math.Round(resultado.Sum(x => x.custosFixos), 2),
                pontoEquilibrio = Math.Round(resultado.Sum(x => x.pontoEquilibrio), 2),
            },
            observacao = "Folha sem loja (FGTS/INSS/provisões e pró-labore) rateada por nº de funcionários de cada loja; demais custos sem loja, pela participação no faturamento."
        });
    }

    /// <summary>Sócio/proprietário (pró-labore, sem FGTS) — excluído da contagem de funcionários CLT.</summary>
    private static bool EhSocio(string? cargo)
    {
        var c = (cargo ?? string.Empty).ToLowerInvariant();
        return c.Contains("sócio") || c.Contains("socio")
            || c.Contains("proprietár") || c.Contains("proprietar")
            || c.Contains("pró-labore") || c.Contains("pro-labore");
    }

    /// <summary>Meta de venda POR DIA para cobrir (a) as despesas operacionais e (b) os
    /// financiamentos do mês — cada uma dividida pela margem de contribuição e pelos dias do mês.</summary>
    [HttpGet("metas-diarias")]
    public async Task<IActionResult> MetasDiarias([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
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
                && (localEstoqueId == null || l.LocalEstoqueId == localEstoqueId)
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

        var mc = await MargemContribuicaoAsync(empresaId, inicio, fim, ct, localEstoqueId);

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

    /// <summary>Necessidade de capital de giro do mês: quanto a COMPRA de estoque do mês
    /// supera o CUSTO das vendas previstas (ritmo dos últimos 30 dias). Positivo = você
    /// compra mais mercadoria do que vende, e a diferença precisa vir de capital de giro.</summary>
    [HttpGet("capital-giro")]
    public async Task<IActionResult> CapitalGiro([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var anoRef = ano ?? DateTime.Today.Year;
        var mesRef = mes ?? DateTime.Today.Month;
        var inicio = new DateTime(anoRef, mesRef, 1);
        var fimExcl = inicio.AddMonths(1);

        var estoqueAPagar = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == Domain.Financeiro.Entities.TipoLancamento.ContaPagar
                && l.Status != Domain.Financeiro.Entities.StatusLancamento.Cancelado
                && l.Categoria == "Custo (CMV)"
                && (localEstoqueId == null || l.LocalEstoqueId == localEstoqueId)
                && l.DataVencimento >= inicio && l.DataVencimento < fimExcl)
            .SumAsync(l => l.ValorOriginal, ct);

        // Vendas previstas = ritmo dos últimos 30 dias (mês corrente real da operação).
        var vendasPrevistas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && (localEstoqueId == null || v.LocalEstoqueId == localEstoqueId)
                && v.DataHora >= DateTime.Today.AddDays(-30))
            .SumAsync(v => (decimal?)v.Total, ct) ?? 0m;

        // Margem de contribuição dos últimos 90 dias (base estável para o % de custo).
        var de90 = DateTime.Today.AddDays(-90);
        var itens90 = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId && x.v.DataHora >= de90
                && (localEstoqueId == null || x.v.LocalEstoqueId == localEstoqueId)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                (x, p) => new { rec = x.i.PrecoUnitario * x.i.Quantidade, cus = p.CustoUnitario * x.i.Quantidade })
            .ToListAsync(ct);
        var rec90 = itens90.Sum(i => i.rec);
        var mc = rec90 > 0 ? Math.Round((rec90 - itens90.Sum(i => i.cus)) / rec90 * 100, 2) : 0m;

        var cmvPrevisto = Math.Round(vendasPrevistas * (1 - mc / 100m), 2);
        var necessidade = Math.Round(estoqueAPagar - cmvPrevisto, 2);

        return Ok(new
        {
            periodo = new { ano = anoRef, mes = mesRef },
            estoqueAPagarMes = estoqueAPagar,
            vendasPrevistas = Math.Round(vendasPrevistas, 2),
            percentualMargemContribuicao = mc,
            cmvPrevisto,
            necessidadeCapitalGiro = necessidade
        });
    }

    /// <summary>Margem de contribuição por CATEGORIA de produto nos últimos N dias — para
    /// afinar a meta de venda (o mix vendido muda a margem média).</summary>
    [HttpGet("margem-categorias")]
    public async Task<IActionResult> MargemCategorias([FromQuery] Guid empresaId,
        [FromQuery] int dias = 90, [FromQuery] Guid? localEstoqueId = null, CancellationToken ct = default)
    {
        var de = DateTime.Today.AddDays(-Math.Abs(dias));
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId && x.v.DataHora >= de
                && (localEstoqueId == null || x.v.LocalEstoqueId == localEstoqueId)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id,
                (x, p) => new { p.CategoriaId, receita = x.i.PrecoUnitario * x.i.Quantidade, custo = p.CustoUnitario * x.i.Quantidade })
            .ToListAsync(ct);

        var catIds = itens.Select(i => i.CategoriaId).Distinct().ToList();
        var nomes = await db.Categorias.AsNoTracking().Where(c => catIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, ct);

        var totalReceita = itens.Sum(i => i.receita);
        var categorias = itens.GroupBy(i => i.CategoriaId)
            .Select(g => new { id = g.Key, rec = g.Sum(x => x.receita), mar = g.Sum(x => x.receita) - g.Sum(x => x.custo) })
            .Select(x => new
            {
                categoria = nomes.TryGetValue(x.id, out var n) ? n : "(sem categoria)",
                receita = Math.Round(x.rec, 2),
                margem = Math.Round(x.mar, 2),
                margemPct = x.rec > 0 ? Math.Round(x.mar / x.rec * 100, 1) : 0m,
                pctFaturamento = totalReceita > 0 ? Math.Round(x.rec / totalReceita * 100, 1) : 0m,
            })
            .OrderByDescending(x => x.receita).ToList();

        var margemMedia = totalReceita > 0
            ? Math.Round((totalReceita - itens.Sum(i => i.custo)) / totalReceita * 100, 2) : 0m;

        return Ok(new { dias = Math.Abs(dias), margemMedia, categorias });
    }

    /// <summary>Margem de contribuição % do mês; se o mês não tem vendas, usa os últimos 90 dias.</summary>
    private async Task<decimal> MargemContribuicaoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct, Guid? localEstoqueId = null)
    {
        async Task<decimal> Calc(DateTime de, DateTime ate)
        {
            var itens = await db.ItensVenda.AsNoTracking()
                .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
                .Where(x => x.v.EmpresaId == empresaId && x.v.DataHora >= de && x.v.DataHora < ate
                    && (localEstoqueId == null || x.v.LocalEstoqueId == localEstoqueId)
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
