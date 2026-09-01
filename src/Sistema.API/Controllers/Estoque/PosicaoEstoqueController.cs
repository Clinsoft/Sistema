using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.API.Extensions;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/estoque")]
[Authorize]
public class PosicaoEstoqueController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Posição atual de estoque com custo total por produto.</summary>
    [HttpGet("posicao")]
    public async Task<IActionResult> Posicao([FromQuery] Guid empresaId,
        [FromQuery] Guid? categoriaId, [FromQuery] bool? abaixoMinimo,
        CancellationToken ct)
    {
        var query = db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo);

        if (categoriaId.HasValue) query = query.Where(p => p.CategoriaId == categoriaId);
        if (abaixoMinimo == true) query = query.Where(p => p.EstoqueAtual <= p.EstoqueMinimo);

        var produtos = await query
            .OrderBy(p => p.Descricao)
            .Select(p => new
            {
                p.Id, p.Codigo, p.Descricao, p.CategoriaId, p.MarcaId,
                p.UnidadeMedidaId, p.EstoqueAtual, p.EstoqueMinimo,
                p.CustoUnitario, p.PrecoVenda,
                CustoTotal = p.EstoqueAtual * p.CustoUnitario,
                ValorVendaTotal = p.EstoqueAtual * p.PrecoVenda,
                AbaixoMinimo = p.EstoqueAtual <= p.EstoqueMinimo
            })
            .ToListAsync(ct);

        return Ok(new
        {
            produtos,
            totais = new
            {
                qtdProdutos = produtos.Count,
                custoTotalEstoque = produtos.Sum(p => p.CustoTotal),
                valorVendaTotalEstoque = produtos.Sum(p => p.ValorVendaTotal),
                qtdAbaixoMinimo = produtos.Count(p => p.AbaixoMinimo)
            }
        });
    }

    /// <summary>
    /// Posição de estoque por LOJA (local). O saldo de cada produto na loja é
    /// reconstruído do histórico de movimentações (que já registra o local),
    /// com o mesmo sinal por tipo usado no estoque global.
    /// </summary>
    [HttpGet("posicao-por-loja")]
    public async Task<IActionResult> PosicaoPorLoja([FromQuery] Guid empresaId,
        [FromQuery] Guid localEstoqueId, [FromQuery] bool somenteComSaldo = true, CancellationToken ct = default)
    {
        localEstoqueId = User.EscoparLoja(localEstoqueId);   // atendente: sempre a própria loja
        var saldos = await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.LocalEstoqueId == localEstoqueId)
            .GroupBy(m => m.ProdutoId)
            .Select(g => new
            {
                produtoId = g.Key,
                saldo = g.Sum(m =>
                    m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Entrada
                    || m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.AjustePositivo
                    || m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Devolucao ? m.Quantidade
                  : m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Saida
                    || m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.AjusteNegativo ? -m.Quantidade
                  : m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Transferencia
                    && m.DocumentoOrigem != null && m.DocumentoOrigem.StartsWith("TRANSF<-") ? m.Quantidade
                  : m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Transferencia ? -m.Quantidade
                  : 0m)
            })
            .ToListAsync(ct);

        var comSaldo = somenteComSaldo ? saldos.Where(s => s.saldo != 0).ToList() : saldos;
        var ids = comSaldo.Select(s => s.produtoId).ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var lista = comSaldo
            .Where(s => produtos.ContainsKey(s.produtoId))
            .Select(s =>
            {
                var p = produtos[s.produtoId];
                return new
                {
                    p.Id, p.Codigo, p.Descricao, p.CategoriaId, p.MarcaId, p.UnidadeMedidaId,
                    saldoLoja = s.saldo,
                    estoqueTotal = p.EstoqueAtual,          // soma de todas as lojas
                    p.EstoqueMinimo, p.CustoUnitario, p.PrecoVenda,
                    custoTotal = s.saldo * p.CustoUnitario,
                    valorVendaTotal = s.saldo * p.PrecoVenda,
                    abaixoMinimo = s.saldo <= p.EstoqueMinimo
                };
            })
            .OrderBy(x => x.Descricao)
            .ToList();

        return Ok(new
        {
            produtos = lista,
            totais = new
            {
                qtdProdutos = lista.Count,
                custoTotalEstoque = lista.Sum(p => p.custoTotal),
                valorVendaTotalEstoque = lista.Sum(p => p.valorVendaTotal),
                qtdAbaixoMinimo = lista.Count(p => p.abaixoMinimo)
            }
        });
    }

    /// <summary>Inventário — posição de estoque por local para contagem física.</summary>
    [HttpGet("inventario")]
    public async Task<IActionResult> Inventario([FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        localEstoqueId = User.EscoparLoja(localEstoqueId);   // atendente: sempre a própria loja
        var query = db.Lotes.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Quantidade > 0);

        if (localEstoqueId.HasValue) query = query.Where(l => l.LocalEstoqueId == localEstoqueId);

        var lotes = await query
            .Join(db.Produtos, l => l.ProdutoId, p => p.Id,
                (l, p) => new
                {
                    p.Codigo, p.Descricao,
                    l.LocalEstoqueId, l.NumeroLote,
                    l.DataValidade, l.Quantidade, l.CustoUnitario,
                    Vencido = l.DataValidade.HasValue && l.DataValidade < DateTime.Today
                })
            .OrderBy(x => x.Descricao).ThenBy(x => x.NumeroLote)
            .ToListAsync(ct);

        return Ok(lotes);
    }

    /// <summary>Curva ABC de produtos por valor de venda no período.</summary>
    /// <summary>Produtos com estoque NEGATIVO — em geral venda sem entrada escriturada
    /// (ou produto duplicado). Marca os que nunca tiveram item de entrada de NF-e.</summary>
    [HttpGet("negativos")]
    public async Task<IActionResult> Negativos([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var itens = await (
            from p in db.Produtos.AsNoTracking()
            join u in db.UnidadesMedida.AsNoTracking() on p.UnidadeMedidaId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            where p.EmpresaId == empresaId && p.Ativo && p.EstoqueAtual < 0
            orderby p.EstoqueAtual
            select new
            {
                p.Id, p.Codigo, p.Descricao,
                p.EstoqueAtual, p.PrecoVenda, p.CodigoBarras,
                UnidadeSigla = u != null ? u.Sigla : "",
                Pesavel = u != null && u.Pesavel,
                TemEntrada = db.ItensEntradaNFe.Any(i => i.ProdutoId == p.Id)
            }).ToListAsync(ct);

        return Ok(new { itens, total = itens.Count });
    }

    [HttpGet("curva-abc")]
    public async Task<IActionResult> CurvaAbc([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        localEstoqueId = User.EscoparLoja(localEstoqueId);   // atendente: sempre a própria loja
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && (localEstoqueId == null || x.v.LocalEstoqueId == localEstoqueId)
                && x.v.DataHora >= inicio.Date && x.v.DataHora < fim.Date.AddDays(1))
            .GroupBy(x => new { x.i.ProdutoId, x.i.Descricao })
            .Select(g => new
            {
                g.Key.ProdutoId,
                g.Key.Descricao,
                TotalVendido = g.Sum(x => x.i.Total),
                QtdVendida = g.Sum(x => x.i.Quantidade)
            })
            .OrderByDescending(x => x.TotalVendido)
            .ToListAsync(ct);

        if (!itens.Any()) return Ok(new { itens, totalGeral = 0m });

        // Custo e estoque atuais do produto (o item de venda não guarda custo):
        // margem estimada = venda − custo atual × qtd vendida.
        var prodIds = itens.Select(i => i.ProdutoId).Distinct().ToList();
        var infoProduto = await db.Produtos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CustoUnitario, p.EstoqueAtual })
            .ToDictionaryAsync(p => p.Id, ct);

        var totalGeral = itens.Sum(i => i.TotalVendido);
        var acumulado = 0m;
        var resultado = itens.Select((item, idx) =>
        {
            acumulado += item.TotalVendido;
            var pct = totalGeral > 0 ? acumulado / totalGeral * 100 : 0;
            infoProduto.TryGetValue(item.ProdutoId, out var p);
            var custoUnit = p?.CustoUnitario ?? 0m;
            var custoVendido = custoUnit * item.QtdVendida;
            var margemValor = item.TotalVendido - custoVendido;
            return new
            {
                item.ProdutoId, item.Descricao,
                item.TotalVendido, item.QtdVendida,
                Participacao = totalGeral > 0 ? item.TotalVendido / totalGeral * 100 : 0,
                ParticipacaoAcumulada = pct,
                Curva = pct <= 80 ? "A" : pct <= 95 ? "B" : "C",
                EstoqueAtual = p?.EstoqueAtual ?? 0m,
                CustoUnitario = custoUnit,
                MargemValor = margemValor,
                MargemPct = item.TotalVendido > 0 ? margemValor / item.TotalVendido * 100 : 0
            };
        });

        return Ok(new { itens = resultado, totalGeral });
    }
}
