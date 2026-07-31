using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

[ApiController]
[Route("api/relatorios/vendas")]
[Authorize]
public class RelatoriosVendasController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Vendas realizadas por período com totais.</summary>
    [HttpGet("realizadas")]
    public async Task<IActionResult> VendasRealizadas(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var vendas = await db.Vendas.AsNoTracking()
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora <= fim
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .OrderByDescending(v => v.DataHora)
            .ToListAsync(ct);

        return Ok(new
        {
            vendas = vendas.Select(v => new
            {
                v.Id, v.Numero, v.DataHora, v.ClienteId,
                v.SubTotal, v.TotalDesconto, v.Total,
                v.TotalPago, v.Troco,
                qtdItens = v.Itens.Count,
                formasPagamento = v.Pagamentos.Select(p => new { p.Forma, p.Valor })
            }),
            totais = new
            {
                qtdVendas = vendas.Count,
                totalBruto = vendas.Sum(v => v.SubTotal),
                totalDesconto = vendas.Sum(v => v.TotalDesconto),
                totalLiquido = vendas.Sum(v => v.Total),
                ticketMedio = vendas.Any() ? vendas.Average(v => v.Total) : 0
            }
        });
    }

    /// <summary>Vendas diárias com ticket médio.</summary>
    [HttpGet("diarias")]
    public async Task<IActionResult> VendasDiarias(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var dias = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio.Date && v.DataHora < fim.Date.AddDays(1)
                && (localEstoqueId == null || v.LocalEstoqueId == localEstoqueId)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .GroupBy(v => v.DataHora.Date)
            .Select(g => new
            {
                data = g.Key,
                qtdVendas = g.Count(),
                totalVendido = g.Sum(v => v.Total),
                ticketMedio = g.Average(v => v.Total)
            })
            .OrderBy(x => x.data)
            .ToListAsync(ct);

        return Ok(dias);
    }

    /// <summary>Vendas por hora (para análise de pico de atendimento).</summary>
    [HttpGet("por-hora")]
    public async Task<IActionResult> VendasPorHora(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var horas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora <= fim
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .GroupBy(v => v.DataHora.Hour)
            .Select(g => new { hora = g.Key, qtdVendas = g.Count(), totalVendido = g.Sum(v => v.Total) })
            .OrderBy(x => x.hora)
            .ToListAsync(ct);

        return Ok(horas);
    }

    /// <summary>Produtos vendidos no período — analítico por item.</summary>
    [HttpGet("produtos-vendidos")]
    public async Task<IActionResult> ProdutosVendidos(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && x.v.DataHora >= inicio && x.v.DataHora <= fim)
            .GroupBy(x => new { x.i.ProdutoId, x.i.Descricao })
            .Select(g => new
            {
                g.Key.ProdutoId, g.Key.Descricao,
                qtdVendida = g.Sum(x => x.i.Quantidade),
                totalVendido = g.Sum(x => x.i.Total),
                ticketMedio = g.Average(x => x.i.PrecoUnitario)
            })
            .OrderByDescending(x => x.totalVendido)
            .ToListAsync(ct);

        return Ok(itens);
    }

    /// <summary>Ranking de vendas por vendedor.</summary>
    [HttpGet("por-vendedor")]
    public async Task<IActionResult> PorVendedor(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var ranking = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && (localEstoqueId == null || v.LocalEstoqueId == localEstoqueId)
                && v.DataHora >= inicio.Date && v.DataHora < fim.Date.AddDays(1))
            // Agrupa pelo VENDEDOR (colaborador que efetuou a venda). Vendas antigas
            // sem VendedorId caem no operador (UsuarioId), preservando o histórico.
            .GroupBy(v => v.VendedorId ?? v.UsuarioId)
            .Select(g => new
            {
                usuarioId = g.Key,
                qtdVendas = g.Count(),
                totalVendido = g.Sum(v => v.Total),
                ticketMedio = g.Average(v => v.Total)
            })
            .OrderByDescending(x => x.totalVendido)
            .ToListAsync(ct);

        return Ok(ranking);
    }

    /// <summary>Ranking de vendas por LOJA/unidade (local de estoque do caixa).</summary>
    [HttpGet("por-loja")]
    public async Task<IActionResult> PorLoja(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var ranking = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && v.DataHora >= inicio.Date && v.DataHora < fim.Date.AddDays(1))
            .GroupBy(v => v.LocalEstoqueId)
            .Select(g => new
            {
                localEstoqueId = g.Key,
                qtdVendas = g.Count(),
                totalVendido = g.Sum(v => v.Total),
                ticketMedio = g.Average(v => v.Total)
            })
            .OrderByDescending(x => x.totalVendido)
            .ToListAsync(ct);

        var ids = ranking.Select(r => r.localEstoqueId).ToList();
        var nomes = await db.LocaisEstoque.AsNoTracking()
            .Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        return Ok(ranking.Select(r => new
        {
            r.localEstoqueId,
            loja = nomes.GetValueOrDefault(r.localEstoqueId, "—"),
            r.qtdVendas, r.totalVendido, r.ticketMedio
        }));
    }

    /// <summary>Devoluções por período.</summary>
    [HttpGet("devolucoes")]
    public async Task<IActionResult> Devolucoes(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var canceladas = await db.Vendas.AsNoTracking()
            .Include(v => v.Itens)
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Cancelada
                && v.DataHora >= inicio && v.DataHora <= fim)
            .OrderByDescending(v => v.DataHora)
            .ToListAsync(ct);

        return Ok(new
        {
            vendas = canceladas.Select(v => new
            {
                v.Id, v.Numero, v.DataHora, v.Total, v.Observacao,
                itens = v.Itens.Select(i => new { i.Descricao, i.Quantidade, i.Total })
            }),
            totalDevolvido = canceladas.Sum(v => v.Total),
            qtd = canceladas.Count
        });
    }

    /// <summary>
    /// Relatório anual de vendas — mês a mês — com projeção para o próximo exercício.
    /// Retorna realizados do ano base, metas projetadas (crescimento %) e acumulados.
    /// </summary>
    [HttpGet("anual")]
    public async Task<IActionResult> Anual(
        [FromQuery] Guid empresaId,
        [FromQuery] int ano,
        [FromQuery] decimal crescimentoMeta = 10m,
        CancellationToken ct = default)
    {
        var nomesMes = new[]
        {
            "Jan","Fev","Mar","Abr","Mai","Jun",
            "Jul","Ago","Set","Out","Nov","Dez"
        };

        // Puxa todas as vendas dos dois anos de uma vez (atual e anterior para comparação YoY)
        var anoAnterior = ano - 1;
        var inicioConsulta = new DateTime(anoAnterior, 1, 1);
        var fimConsulta   = new DateTime(ano, 12, 31, 23, 59, 59);

        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && v.DataHora >= inicioConsulta && v.DataHora <= fimConsulta)
            .Select(v => new { v.DataHora, v.Total, v.TotalDesconto, v.SubTotal })
            .ToListAsync(ct);

        var meses = Enumerable.Range(1, 12).Select(mes =>
        {
            var realizadoAno     = vendas.Where(v => v.DataHora.Year == ano     && v.DataHora.Month == mes).ToList();
            var realizadoAnoAnt  = vendas.Where(v => v.DataHora.Year == anoAnterior && v.DataHora.Month == mes).ToList();

            var totalAno    = realizadoAno.Sum(v => v.Total);
            var totalAnoAnt = realizadoAnoAnt.Sum(v => v.Total);
            var qtdAno      = realizadoAno.Count;
            var qtdAnoAnt   = realizadoAnoAnt.Count;

            // Meta do próximo exercício = realizado do ano base × (1 + crescimento%)
            var meta = Math.Round(totalAno * (1 + crescimentoMeta / 100), 2);

            // Variação YoY (ano atual vs anterior)
            var variacaoYoy = totalAnoAnt > 0
                ? Math.Round((totalAno - totalAnoAnt) / totalAnoAnt * 100, 2)
                : (totalAno > 0 ? 100m : 0m);

            return new
            {
                mes,
                nomeMes = nomesMes[mes - 1],
                // Ano atual (base do planejamento)
                realizado        = totalAno,
                qtdVendas        = qtdAno,
                ticketMedio      = qtdAno > 0 ? Math.Round(totalAno / qtdAno, 2) : 0m,
                totalDesconto    = realizadoAno.Sum(v => v.TotalDesconto),
                // Ano anterior (comparação histórica)
                realizadoAnoAnt  = totalAnoAnt,
                qtdVendasAnoAnt  = qtdAnoAnt,
                variacaoYoy,
                // Projeção próximo exercício
                meta,
                crescimentoMeta,
                atingiuMeta      = totalAno >= meta   // sempre false para ano futuro; útil para meses já passados vs meta revisada
            };
        }).ToList();

        // Acumulados do ano
        var totalRealizadoAno    = meses.Sum(m => (decimal)((dynamic)m).realizado);
        var totalRealizadoAnoAnt = meses.Sum(m => (decimal)((dynamic)m).realizadoAnoAnt);
        var totalMeta            = meses.Sum(m => (decimal)((dynamic)m).meta);
        var qtdTotalAno          = meses.Sum(m => (int)((dynamic)m).qtdVendas);
        var ticketMedioGeral     = qtdTotalAno > 0 ? Math.Round(totalRealizadoAno / qtdTotalAno, 2) : 0m;
        var variacaoTotalYoy     = totalRealizadoAnoAnt > 0
            ? Math.Round((totalRealizadoAno - totalRealizadoAnoAnt) / totalRealizadoAnoAnt * 100, 2)
            : 0m;

        // Acumulado mês a mês (útil para gráfico de linha)
        decimal acumAno = 0, acumMeta = 0;
        var acumulados = meses.Select(m =>
        {
            acumAno  += (decimal)((dynamic)m).realizado;
            acumMeta += (decimal)((dynamic)m).meta;
            return new { mes = (int)((dynamic)m).mes, acumAno, acumMeta };
        }).ToList();

        return Ok(new
        {
            ano,
            anoAnterior,
            crescimentoMeta,
            meses,
            acumulados,
            totais = new
            {
                totalRealizadoAno,
                totalRealizadoAnoAnt,
                totalMeta,
                qtdTotalAno,
                ticketMedioGeral,
                variacaoTotalYoy,
                crescimentoNecessario = totalRealizadoAno > 0
                    ? Math.Round((totalMeta - totalRealizadoAno) / totalRealizadoAno * 100, 2)
                    : crescimentoMeta
            }
        });
    }
}
