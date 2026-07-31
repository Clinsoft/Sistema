using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

[ApiController]
[Route("api/relatorios/estoque")]
[Authorize]
public class RelatoriosEstoqueController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Movimentação de estoque por produto e período.</summary>
    [HttpGet("movimentacao")]
    public async Task<IActionResult> Movimentacao(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] Guid? produtoId, CancellationToken ct)
    {
        var query = db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.CriadoEm >= inicio && m.CriadoEm < fim.AddDays(1));
        if (produtoId.HasValue) query = query.Where(m => m.ProdutoId == produtoId);

        var movs = await query
            .Join(db.Produtos, m => m.ProdutoId, p => p.Id,
                (m, p) => new { m, produtoDescricao = p.Descricao, produtoCodigo = p.Codigo })
            .OrderByDescending(x => x.m.CriadoEm)
            .Select(x => new
            {
                x.m.Id, x.m.ProdutoId, x.produtoCodigo, x.produtoDescricao,
                x.m.LocalEstoqueId, x.m.Tipo, x.m.Quantidade,
                x.m.CustoUnitario, x.m.DocumentoOrigem, x.m.CriadoEm
            })
            .ToListAsync(ct);

        return Ok(movs);
    }

    /// <summary>Markup e custo de produtos — comparativo custo × preço × margem.</summary>
    [HttpGet("markup-custo")]
    public async Task<IActionResult> MarkupCusto([FromQuery] Guid empresaId,
        [FromQuery] Guid? categoriaId, CancellationToken ct)
    {
        var query = db.Produtos.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Ativo);
        if (categoriaId.HasValue) query = query.Where(p => p.CategoriaId == categoriaId);

        var produtos = await query
            .OrderBy(p => p.Descricao)
            .Select(p => new
            {
                p.Id, p.Codigo, p.Descricao, p.CategoriaId,
                p.CustoUnitario, p.PrecoVenda, p.PrecoAtacado,
                p.Markup, p.MargemLucro, p.EstoqueAtual,
                CustoTotalEstoque = p.EstoqueAtual * p.CustoUnitario
            })
            .ToListAsync(ct);

        return Ok(produtos);
    }

    /// <summary>Validades — produtos com lotes vencidos ou próximos do vencimento.</summary>
    [HttpGet("validades")]
    public async Task<IActionResult> Validades([FromQuery] Guid empresaId,
        [FromQuery] int diasAlerta = 30, CancellationToken ct = default)
    {
        var limite = DateTime.Today.AddDays(diasAlerta);
        var lotes = await db.Lotes.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Quantidade > 0
                && l.DataValidade.HasValue && l.DataValidade <= limite)
            .Join(db.Produtos, l => l.ProdutoId, p => p.Id,
                (l, p) => new
                {
                    l.ProdutoId, p.Codigo, p.Descricao,
                    l.NumeroLote, l.DataValidade, l.Quantidade,
                    l.LocalEstoqueId,
                    DiasParaVencer = (int)(l.DataValidade!.Value - DateTime.Today).TotalDays,
                    Vencido = l.DataValidade < DateTime.Today
                })
            .OrderBy(x => x.DataValidade)
            .ToListAsync(ct);

        return Ok(lotes);
    }

    /// <summary>Estoque faturado — saídas por venda no período.</summary>
    [HttpGet("faturado")]
    public async Task<IActionResult> EstoqueFaturado([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var saidas = await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId
                && m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Saida
                && m.CriadoEm >= inicio && m.CriadoEm < fim.AddDays(1))
            .Join(db.Produtos, m => m.ProdutoId, p => p.Id,
                (m, p) => new
                {
                    m.ProdutoId, p.Codigo, p.Descricao,
                    m.Quantidade, m.CustoUnitario,
                    CustoTotal = m.Quantidade * m.CustoUnitario,
                    m.DocumentoOrigem, m.CriadoEm
                })
            .OrderByDescending(x => x.CriadoEm)
            .ToListAsync(ct);

        return Ok(new
        {
            saidas,
            totalCmv = saidas.Sum(s => s.CustoTotal)
        });
    }

    /// <summary>Livro de inventário (P7) — posição completa com custo total.</summary>
    [HttpGet("livro-inventario")]
    public async Task<IActionResult> LivroInventario([FromQuery] Guid empresaId,
        [FromQuery] DateTime? dataReferencia, CancellationToken ct)
    {
        var data = dataReferencia?.Date ?? DateTime.Today;

        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo)
            .Join(db.Categorias, p => p.CategoriaId, c => c.Id,
                (p, c) => new
                {
                    p.Id, p.Codigo, p.Descricao,
                    Categoria = c.Nome, p.Ncm,
                    p.CustoUnitario, p.EstoqueAtual,
                    Total = p.EstoqueAtual * p.CustoUnitario
                })
            .OrderBy(x => x.Descricao)
            .ToListAsync(ct);

        return Ok(new
        {
            dataReferencia = data,
            produtos,
            totalGeral = produtos.Sum(p => p.Total),
            qtdItens = produtos.Count
        });
    }
}
