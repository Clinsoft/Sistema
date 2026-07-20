using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>Inventário — posição de estoque por local para contagem física.</summary>
    [HttpGet("inventario")]
    public async Task<IActionResult> Inventario([FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
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
    [HttpGet("curva-abc")]
    public async Task<IActionResult> CurvaAbc([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
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

        var totalGeral = itens.Sum(i => i.TotalVendido);
        var acumulado = 0m;
        var resultado = itens.Select((item, idx) =>
        {
            acumulado += item.TotalVendido;
            var pct = totalGeral > 0 ? acumulado / totalGeral * 100 : 0;
            return new
            {
                item.ProdutoId, item.Descricao,
                item.TotalVendido, item.QtdVendida,
                Participacao = totalGeral > 0 ? item.TotalVendido / totalGeral * 100 : 0,
                ParticipacaoAcumulada = pct,
                Curva = pct <= 80 ? "A" : pct <= 95 ? "B" : "C"
            };
        });

        return Ok(new { itens = resultado, totalGeral });
    }
}
