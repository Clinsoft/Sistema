using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Publico;

[ApiController]
[Route("api/publico/produtos")]
public class ProdutosPublicosController(SistemaDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var produto = await db.Produtos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.Ativo, ct);
        if (produto is null) return NotFound();

        var categoria = await db.Categorias.AsNoTracking()
            .Where(c => c.Id == produto.CategoriaId)
            .Select(c => c.Nome).FirstOrDefaultAsync(ct);
        var marca = await db.Marcas.AsNoTracking()
            .Where(m => m.Id == produto.MarcaId)
            .Select(m => m.Nome).FirstOrDefaultAsync(ct);
        var tabela = await db.TabelasNutricionais.AsNoTracking()
            .FirstOrDefaultAsync(t => t.ProdutoId == id, ct);
        var receitas = await db.ReceitasProduto.AsNoTracking()
            .Where(r => r.ProdutoId == id).ToListAsync(ct);
        var sugestoes = await db.SugestoesProduto.AsNoTracking()
            .Where(s => s.ProdutoId == id).ToListAsync(ct);

        return Ok(new
        {
            produto.Id, produto.Codigo, produto.Descricao, produto.DescricaoComplementar,
            produto.CodigoBarras, produto.PrecoVenda, produto.Ncm,
            categoria, marca,
            tabelaNutricional = tabela is null ? null : new
            {
                tabela.Porcao, tabela.Calorias, tabela.Proteinas,
                tabela.CarboidratosTotais, tabela.GordurasTotais,
                tabela.GordurasSaturadas, tabela.GordurasTrans,
                tabela.FibrasDieteticas, tabela.Sodio, tabela.Acucares,
            },
            receitas = receitas.Select(r => new
            {
                r.Titulo, r.Descricao, r.ModoPreparo, r.Ingredientes,
                tempoPreparoMin = r.TempoPreparoMinutos,
                r.Porcoes, r.Dicas,
            }),
            sugestoes = sugestoes.Select(s => new { s.Titulo, s.Descricao }),
        });
    }

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar([FromQuery] Guid empresaId, [FromQuery] string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<object>());
        var lista = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo
                && (p.Descricao.Contains(q) || (p.CodigoBarras != null && p.CodigoBarras.Contains(q))))
            .OrderBy(p => p.Descricao).Take(10)
            .Select(p => new { p.Id, p.Descricao, p.PrecoVenda, p.CodigoBarras })
            .ToListAsync(ct);
        return Ok(lista);
    }
}
