using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Indicadores dos 4 cards do topo do dashboard.</summary>
    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] Guid empresaId,
        [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var hoje = DateTime.Today;
        var amanha = hoje.AddDays(1);

        var vendasHoje = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == StatusVenda.Finalizada
                && (localEstoqueId == null || v.LocalEstoqueId == localEstoqueId)
                && v.DataHora >= hoje && v.DataHora < amanha)
            .SumAsync(v => (decimal?)v.Total, ct) ?? 0m;

        var pedidosAbertos = await db.PedidosCompra.AsNoTracking()
            .CountAsync(p => p.EmpresaId == empresaId
                && p.Status == StatusPedidoCompra.Enviado, ct);

        var aReceberVencido = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaReceber
                && l.Status == StatusLancamento.EmAberto
                && l.DataVencimento < amanha)
            .SumAsync(l => (decimal?)(l.ValorOriginal - l.ValorPago), ct) ?? 0m;

        var produtosSemEstoque = await db.Produtos.AsNoTracking()
            .CountAsync(p => p.EmpresaId == empresaId && p.Ativo && p.EstoqueAtual <= 0, ct);

        return Ok(new
        {
            vendasHoje,
            pedidosAbertos,
            aReceberVencido,
            produtosSemEstoque,
        });
    }

    /// <summary>Movimento de vendas por dia da semana × hora, separado por loja
    /// (para descobrir os horários/dias de pico).</summary>
    [HttpGet("movimento")]
    public async Task<IActionResult> Movimento([FromQuery] Guid empresaId,
        [FromQuery] int dias = 90, CancellationToken ct = default)
    {
        if (dias < 7) dias = 7;
        if (dias > 365) dias = 365;
        var inicio = DateTime.Today.AddDays(-dias);

        // Puxa só o necessário e agrega em memória (EF não traduz DateTime.DayOfWeek pro SQL).
        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == StatusVenda.Finalizada
                && v.DataHora >= inicio)
            .Select(v => new { v.LocalEstoqueId, v.DataHora, v.Total })
            .ToListAsync(ct);

        var brutos = vendas
            .GroupBy(v => new { v.LocalEstoqueId, Dia = (int)v.DataHora.DayOfWeek, Hora = v.DataHora.Hour })
            .Select(g => new
            {
                g.Key.LocalEstoqueId,
                Dia = g.Key.Dia,              // 0=Domingo … 6=Sábado
                Hour = g.Key.Hora,
                Vendas = g.Count(),
                Faturamento = g.Sum(x => x.Total)
            })
            .ToList();

        var nomes = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId)
            .ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        var diasSemana = new[] { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" };
        var diasCurto = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

        var lojas = brutos
            .GroupBy(x => x.LocalEstoqueId)
            .Select(g =>
            {
                var totalVendas = g.Sum(x => x.Vendas);
                var faturamento = g.Sum(x => x.Faturamento);

                var porDia = Enumerable.Range(0, 7).Select(d =>
                {
                    var itens = g.Where(x => x.Dia == d).ToList();
                    return new { dia = d, label = diasCurto[d], vendas = itens.Sum(x => x.Vendas), faturamento = itens.Sum(x => x.Faturamento) };
                }).ToList();

                var porHora = g.GroupBy(x => x.Hour).OrderBy(h => h.Key)
                    .Select(h => new { hora = h.Key, vendas = h.Sum(x => x.Vendas), faturamento = h.Sum(x => x.Faturamento) })
                    .ToList();

                var picoDiaRaw = porDia.OrderByDescending(d => d.vendas).First();
                var picoHoraRaw = porHora.OrderByDescending(h => h.vendas).FirstOrDefault();

                return new
                {
                    localEstoqueId = g.Key,
                    nome = nomes.TryGetValue(g.Key, out var n) ? n : "Loja",
                    totalVendas,
                    faturamento,
                    picoDia = new { picoDiaRaw.dia, label = diasSemana[picoDiaRaw.dia], picoDiaRaw.vendas, picoDiaRaw.faturamento },
                    picoHora = picoHoraRaw == null ? null
                        : new { picoHoraRaw.hora, label = $"{picoHoraRaw.hora}h", picoHoraRaw.vendas, picoHoraRaw.faturamento },
                    porDia,
                    porHora,
                    heatmap = g.Select(x => new { dia = x.Dia, hora = x.Hour, vendas = x.Vendas, faturamento = x.Faturamento }).ToList()
                };
            })
            .OrderByDescending(l => l.totalVendas)
            .ToList();

        return Ok(new { periodoDias = dias, lojas });
    }

    /// <summary>Resumo gerencial de um dia: vendas, ticket, margem por loja e contas
    /// (a pagar/receber que vencem no dia) — para o gestor acompanhar/enviar por WhatsApp.</summary>
    [HttpGet("resumo-dia")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
    public async Task<IActionResult> ResumoDia([FromQuery] Guid empresaId,
        [FromQuery] DateTime? data, CancellationToken ct)
    {
        var dia = (data ?? DateTime.Today).Date;
        var amanha = dia.AddDays(1);

        var nomes = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId)
            .ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        var vendasDia = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                     && v.DataHora >= dia && v.DataHora < amanha)
            .GroupBy(v => v.LocalEstoqueId)
            .Select(g => new { Loja = g.Key, Total = g.Sum(x => x.Total), Qtd = g.Count() })
            .ToListAsync(ct);

        // Custo (para margem) por loja, a partir dos itens do dia
        var custoPorLoja = (await (
            from i in db.ItensVenda.AsNoTracking()
            join v in db.Vendas.AsNoTracking() on i.VendaId equals v.Id
            join p in db.Produtos.AsNoTracking() on i.ProdutoId equals p.Id
            where v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                  && v.DataHora >= dia && v.DataHora < amanha
            group new { i, p } by v.LocalEstoqueId into g
            select new { Loja = g.Key, Receita = g.Sum(x => x.i.Total), Custo = g.Sum(x => x.p.CustoUnitario * x.i.Quantidade) }
        ).ToListAsync(ct)).ToDictionary(x => x.Loja);

        var porLoja = vendasDia.Select(v =>
        {
            custoPorLoja.TryGetValue(v.Loja, out var c);
            var receita = c?.Receita ?? 0m; var custo = c?.Custo ?? 0m;
            return new
            {
                nome = nomes.TryGetValue(v.Loja, out var n) ? n : "Loja",
                total = Math.Round(v.Total, 2),
                numeroVendas = v.Qtd,
                ticketMedio = v.Qtd > 0 ? Math.Round(v.Total / v.Qtd, 2) : 0m,
                margemPct = receita > 0 ? Math.Round((receita - custo) / receita * 100, 1) : 0m
            };
        }).OrderByDescending(x => x.total).ToList();

        var totalDia = porLoja.Sum(x => x.total);
        var qtdDia = porLoja.Sum(x => x.numeroVendas);

        var aPagarHoje = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaPagar
                     && l.Status == StatusLancamento.EmAberto
                     && l.DataVencimento >= dia && l.DataVencimento < amanha)
            .SumAsync(l => (decimal?)(l.ValorOriginal - l.ValorPago), ct) ?? 0m;

        var aReceberHoje = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaReceber
                     && l.Status == StatusLancamento.EmAberto
                     && l.DataVencimento >= dia && l.DataVencimento < amanha)
            .SumAsync(l => (decimal?)(l.ValorOriginal - l.ValorPago), ct) ?? 0m;

        return Ok(new
        {
            data = dia,
            totalVendas = Math.Round(totalDia, 2),
            numeroVendas = qtdDia,
            ticketMedio = qtdDia > 0 ? Math.Round(totalDia / qtdDia, 2) : 0m,
            porLoja,
            aPagarHoje = Math.Round(aPagarHoje, 2),
            aReceberHoje = Math.Round(aReceberHoje, 2)
        });
    }

    /// <summary>Projeção da meta de vendas do mês: meta × realizado até hoje × projeção
    /// pelo ritmo atual, com quanto falta e quanto vender por dia nos dias restantes.</summary>
    [HttpGet("projecao-meta")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
    public async Task<IActionResult> ProjecaoMeta([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, CancellationToken ct)
    {
        var hoje = DateTime.Today;
        var a = ano ?? hoje.Year;
        var m = mes ?? hoje.Month;
        var inicioMes = new DateTime(a, m, 1);
        var fimMesEx = inicioMes.AddMonths(1);
        var diasNoMes = (fimMesEx - inicioMes).Days;

        // dias decorridos: se for o mês corrente, até hoje; se mês passado, o mês todo
        var ehMesCorrente = a == hoje.Year && m == hoje.Month;
        var diasDecorridos = ehMesCorrente ? hoje.Day : diasNoMes;
        var diasRestantes = Math.Max(0, diasNoMes - diasDecorridos);

        var realizado = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                     && v.DataHora >= inicioMes && v.DataHora < fimMesEx)
            .SumAsync(v => (decimal?)v.Total, ct) ?? 0m;

        var meta = await db.MetasVendaMensal.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Ano == a && x.Mes == m)
            .Select(x => (decimal?)x.Valor).FirstOrDefaultAsync(ct);

        var mediaDia = diasDecorridos > 0 ? realizado / diasDecorridos : 0m;
        var projecao = Math.Round(mediaDia * diasNoMes, 2);
        var falta = meta.HasValue ? Math.Max(0m, meta.Value - realizado) : (decimal?)null;

        return Ok(new
        {
            ano = a, mes = m,
            meta,                                   // null = meta não definida
            realizado = Math.Round(realizado, 2),
            diasNoMes, diasDecorridos, diasRestantes,
            mediaDiaria = Math.Round(mediaDia, 2),
            projecao,
            pctAtingido = meta is > 0 ? Math.Round(realizado / meta.Value * 100, 1) : (decimal?)null,
            pctProjetado = meta is > 0 ? Math.Round(projecao / meta.Value * 100, 1) : (decimal?)null,
            faltaParaMeta = falta,
            metaDiariaRestante = (falta.HasValue && diasRestantes > 0) ? Math.Round(falta.Value / diasRestantes, 2) : (decimal?)null,
            noRitmoBatiMeta = meta is > 0 ? projecao >= meta.Value : (bool?)null
        });
    }

    /// <summary>Clientes que compravam mas sumiram: última compra há mais de `diasSem`
    /// dias, com total já gasto e telefone — para reativar (ex.: WhatsApp).</summary>
    [HttpGet("clientes-sumidos")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
    public async Task<IActionResult> ClientesSumidos([FromQuery] Guid empresaId,
        [FromQuery] int diasSem = 60, [FromQuery] Guid? localEstoqueId = null, CancellationToken ct = default)
    {
        if (diasSem < 1) diasSem = 60;
        var corte = DateTime.Today.AddDays(-diasSem);

        var porCliente = (await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada && v.ClienteId != null)
            .GroupBy(v => v.ClienteId!.Value)
            .Select(g => new { ClienteId = g.Key, Ultima = g.Max(v => v.DataHora), Total = g.Sum(v => v.Total), Qtd = g.Count() })
            .ToListAsync(ct))
            .Where(x => x.Ultima < corte)
            .ToDictionary(x => x.ClienteId);

        var ids = porCliente.Keys.ToList();
        if (ids.Count == 0) return Ok(new { diasSem, itens = Array.Empty<object>() });

        var nomesLoja = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId)
            .ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo && ids.Contains(c.Id)
                     && (c.Celular != null || c.Telefone != null)
                     && (localEstoqueId == null || c.LocalEstoqueId == localEstoqueId))
            .Select(c => new { c.Id, c.Nome, c.Celular, c.Telefone, c.LocalEstoqueId })
            .ToListAsync(ct);

        var hoje = DateTime.Today;
        var itens = clientes.Select(c =>
        {
            var d = porCliente[c.Id];
            return new
            {
                c.Id, c.Nome,
                telefone = string.IsNullOrWhiteSpace(c.Celular) ? c.Telefone : c.Celular,
                loja = c.LocalEstoqueId.HasValue && nomesLoja.TryGetValue(c.LocalEstoqueId.Value, out var ln) ? ln : "—",
                ultimaCompra = d.Ultima,
                diasSemComprar = (int)(hoje - d.Ultima.Date).TotalDays,
                totalGasto = d.Total,
                numeroCompras = d.Qtd,
                ticketMedio = d.Qtd > 0 ? d.Total / d.Qtd : 0m
            };
        })
        .OrderByDescending(x => x.totalGasto)
        .ToList();

        return Ok(new { diasSem, itens });
    }

    /// <summary>Comparativo gerencial entre lojas no período: faturamento, nº de vendas,
    /// ticket médio, margem e crescimento vs período anterior + top produtos por loja.</summary>
    [HttpGet("comparativo-lojas")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
    public async Task<IActionResult> ComparativoLojas([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var ini = inicio.Date;
        var fimEx = fim.Date.AddDays(1);
        var dias = Math.Max(1, (fimEx - ini).Days);
        var iniAnt = ini.AddDays(-dias);   // período anterior do mesmo tamanho

        var nomes = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId)
            .ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        // Vendas do período (por loja) e do período anterior (para crescimento)
        var atual = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                     && v.DataHora >= ini && v.DataHora < fimEx)
            .GroupBy(v => v.LocalEstoqueId)
            .Select(g => new { Loja = g.Key, Fat = g.Sum(v => v.Total), Qtd = g.Count() })
            .ToListAsync(ct);

        var fatAnt = (await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                     && v.DataHora >= iniAnt && v.DataHora < ini)
            .GroupBy(v => v.LocalEstoqueId)
            .Select(g => new { Loja = g.Key, Fat = g.Sum(v => v.Total) })
            .ToListAsync(ct)).ToDictionary(x => x.Loja, x => x.Fat);

        // Itens vendidos por loja/produto (base para margem e top produtos)
        var porProduto = await (
            from i in db.ItensVenda.AsNoTracking()
            join v in db.Vendas.AsNoTracking() on i.VendaId equals v.Id
            where v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                  && v.DataHora >= ini && v.DataHora < fimEx
            group i by new { v.LocalEstoqueId, i.ProdutoId, i.Descricao } into g
            select new
            {
                g.Key.LocalEstoqueId, g.Key.ProdutoId, g.Key.Descricao,
                Total = g.Sum(x => x.Total), Qtd = g.Sum(x => x.Quantidade)
            }).ToListAsync(ct);

        var custos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId)
            .Select(p => new { p.Id, p.CustoUnitario })
            .ToDictionaryAsync(p => p.Id, p => p.CustoUnitario, ct);

        decimal Custo(Guid? pid, decimal qtd) =>
            (pid.HasValue && custos.TryGetValue(pid.Value, out var c) ? c : 0m) * qtd;

        var lojas = atual.Select(a =>
        {
            var itensLoja = porProduto.Where(x => x.LocalEstoqueId == a.Loja).ToList();
            var receita = itensLoja.Sum(x => x.Total);
            var custoTotal = itensLoja.Sum(x => Custo(x.ProdutoId, x.Qtd));
            var margemValor = receita - custoTotal;
            var anterior = fatAnt.TryGetValue(a.Loja, out var fa) ? fa : 0m;

            return new
            {
                localEstoqueId = a.Loja,
                nome = nomes.TryGetValue(a.Loja, out var n) ? n : "Loja",
                faturamento = a.Fat,
                numeroVendas = a.Qtd,
                ticketMedio = a.Qtd > 0 ? a.Fat / a.Qtd : 0m,
                margemValor,
                margemPct = receita > 0 ? margemValor / receita * 100 : 0m,
                faturamentoAnterior = anterior,
                crescimentoPct = anterior > 0 ? (a.Fat - anterior) / anterior * 100 : (decimal?)null,
                topProdutos = itensLoja.OrderByDescending(x => x.Total).Take(5)
                    .Select(x => new { x.Descricao, total = x.Total, qtd = x.Qtd }).ToList()
            };
        })
        .OrderByDescending(l => l.faturamento)
        .ToList();

        return Ok(new { periodoDias = dias, lojas });
    }
}
