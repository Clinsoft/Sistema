using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

[ApiController]
[Route("api/categorias")]
[Authorize]
public class CategoriasController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.Categorias.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .OrderBy(c => c.Nome)
            .Select(c => new
            {
                c.Id, c.Nome, c.CategoriaPaiId,
                categoriaPaiNome = db.Categorias
                    .Where(p => p.Id == c.CategoriaPaiId).Select(p => p.Nome).FirstOrDefault(),
                qtdProdutos = db.Produtos.Count(p => p.CategoriaId == c.Id && p.Ativo)
            })
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CategoriaRequest req, CancellationToken ct)
    {
        var cat = Categoria.Criar(req.EmpresaId, req.Nome, req.CategoriaPaiId);
        db.Categorias.Add(cat);
        await uow.SalvarAsync(ct);
        return Ok(new { cat.Id, cat.Nome });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CategoriaRequest req, CancellationToken ct)
    {
        var rows = await db.Categorias
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Nome, req.Nome)
                .SetProperty(c => c.CategoriaPaiId, req.CategoriaPaiId), ct);
        return rows > 0 ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var cat = await db.Categorias.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Categoria não encontrada.");

        var qtdProdutos = await db.Produtos.CountAsync(p => p.CategoriaId == id && p.Ativo, ct);
        if (qtdProdutos > 0)
            return BadRequest(new { mensagem = $"Categoria possui {qtdProdutos} produto(s) vinculado(s). Reatribua-os a outra categoria antes de excluir." });

        var temFilhas = await db.Categorias.AnyAsync(c => c.CategoriaPaiId == id, ct);
        if (temFilhas)
            return BadRequest(new { mensagem = "Categoria possui subcategorias. Exclua ou reatribua as subcategorias primeiro." });

        db.Categorias.Remove(cat);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CategoriaRequest(Guid EmpresaId, string Nome, Guid? CategoriaPaiId = null);
