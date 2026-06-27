using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

[ApiController]
[Route("api/relatorios/fornecedores")]
[Authorize]
public class RelatoriosFornecedoresController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Ranking de fornecedores por volume de compras no período.</summary>
    [HttpGet("ranking")]
    public async Task<IActionResult> Ranking([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var pedidos = await db.PedidosCompra.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId
                && p.DataPedido >= inicio && p.DataPedido <= fim.AddDays(1)
                && (p.Status == Domain.Compras.Entities.StatusPedidoCompra.Recebido
                    || p.Status == Domain.Compras.Entities.StatusPedidoCompra.Enviado))
            .Join(db.Fornecedores, p => p.FornecedorId, f => f.Id,
                (p, f) => new { p, FornecedorNome = f.RazaoSocial, f.Cnpj })
            .GroupBy(x => new { x.p.FornecedorId, x.FornecedorNome, x.Cnpj })
            .Select(g => new
            {
                g.Key.FornecedorId, g.Key.FornecedorNome, g.Key.Cnpj,
                qtdPedidos = g.Count(),
                totalComprado = g.Sum(x => x.p.Total)
            })
            .OrderByDescending(x => x.totalComprado)
            .ToListAsync(ct);

        var totalGeral = pedidos.Sum(p => p.totalComprado);
        decimal acumulado = 0;

        return Ok(new
        {
            periodo = new { inicio, fim },
            totalGeral,
            fornecedores = pedidos.Select((p, i) =>
            {
                acumulado += p.totalComprado;
                var percentualAcumulado = totalGeral > 0 ? acumulado / totalGeral * 100 : 0;
                return new
                {
                    posicao = i + 1,
                    p.FornecedorId, p.FornecedorNome, p.Cnpj,
                    p.qtdPedidos, p.totalComprado,
                    percentual = totalGeral > 0 ? Math.Round(p.totalComprado / totalGeral * 100, 2) : 0m,
                    curvaAbc = percentualAcumulado <= 80 ? "A" : percentualAcumulado <= 95 ? "B" : "C"
                };
            })
        });
    }

    /// <summary>Vendas agrupadas por categoria no período.</summary>
    [HttpGet("por-categoria")]
    public async Task<IActionResult> PorCategoria([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.DataHora >= inicio && x.v.DataHora <= fim.AddDays(1)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id, (x, p) => new { x.i, x.v, p })
            .Join(db.Categorias, x => x.p.CategoriaId, c => c.Id,
                (x, c) => new { x.i, CategoriaNome = c.Nome })
            .GroupBy(x => x.CategoriaNome)
            .Select(g => new
            {
                categoria = g.Key,
                qtdVendas = g.Count(),
                totalFaturado = g.Sum(x => x.i.Total)
            })
            .OrderByDescending(x => x.totalFaturado)
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            categorias = itens,
            totalGeral = itens.Sum(i => i.totalFaturado)
        });
    }

    /// <summary>Vendas agrupadas por marca no período.</summary>
    [HttpGet("por-marca")]
    public async Task<IActionResult> PorMarca([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.DataHora >= inicio && x.v.DataHora <= fim.AddDays(1)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .Join(db.Produtos, x => x.i.ProdutoId, p => p.Id, (x, p) => new { x.i, x.v, p })
            .Join(db.Marcas, x => x.p.MarcaId, m => m.Id,
                (x, m) => new { x.i, MarcaNome = m.Nome })
            .GroupBy(x => x.MarcaNome)
            .Select(g => new
            {
                marca = g.Key,
                qtdVendas = g.Count(),
                totalFaturado = g.Sum(x => x.i.Total)
            })
            .OrderByDescending(x => x.totalFaturado)
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            marcas = itens,
            totalGeral = itens.Sum(i => i.totalFaturado)
        });
    }
}
