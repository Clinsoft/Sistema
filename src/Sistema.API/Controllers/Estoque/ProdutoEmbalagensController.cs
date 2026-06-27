using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/produtos/{produtoId:guid}/embalagens")]
[Authorize]
public class ProdutoEmbalagensController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(Guid produtoId, CancellationToken ct)
        => Ok(await db.ProdutosEmbalagem.AsNoTracking()
            .Where(e => e.ProdutoId == produtoId && e.Ativo)
            .OrderBy(e => e.Multiplicador)
            .Select(e => new {
                e.Id, e.Descricao, e.UnidadeMedidaId, e.Multiplicador,
                e.CodigoBarras, e.PrecoVenda, e.Ativo
            })
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Criar(Guid produtoId,
        [FromBody] EmbalagemRequest req, CancellationToken ct)
    {
        var emb = ProdutoEmbalagem.Criar(produtoId, req.UnidadeMedidaId,
            req.Descricao, req.Multiplicador, req.CodigoBarras, req.PrecoVenda);
        db.ProdutosEmbalagem.Add(emb);
        await uow.SalvarAsync(ct);
        return Ok(new { emb.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid produtoId, Guid id,
        [FromBody] EmbalagemRequest req, CancellationToken ct)
    {
        var emb = await db.ProdutosEmbalagem
            .FirstOrDefaultAsync(e => e.Id == id && e.ProdutoId == produtoId, ct)
            ?? throw new KeyNotFoundException();
        emb.Editar(req.UnidadeMedidaId, req.Descricao, req.Multiplicador,
            req.CodigoBarras, req.PrecoVenda, true);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid produtoId, Guid id, CancellationToken ct)
    {
        await db.ProdutosEmbalagem
            .Where(e => e.Id == id && e.ProdutoId == produtoId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Ativo, false), ct);
        return NoContent();
    }
}

public record EmbalagemRequest(
    Guid UnidadeMedidaId, string Descricao, decimal Multiplicador,
    string? CodigoBarras = null, decimal? PrecoVenda = null);
