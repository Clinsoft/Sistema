using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/categorias-financeiras")]
[Authorize]
public class CategoriasFinanceirasController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] string? tipo, CancellationToken ct)
    {
        var query = db.CategoriasFinanceiras.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo);

        if (!string.IsNullOrEmpty(tipo) && Enum.TryParse<TipoCategoria>(tipo, out var t))
            query = query.Where(c => c.Tipo == t);

        return Ok(await query.OrderBy(c => c.Nome).ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarCategoriaRequest req, CancellationToken ct)
    {
        var tipo = Enum.Parse<TipoCategoria>(req.Tipo);
        var cat = CategoriaFinanceira.Criar(req.EmpresaId, req.Nome, tipo, req.Cor);
        db.CategoriasFinanceiras.Add(cat);
        await uow.SalvarAsync(ct);
        return Ok(new { cat.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarCategoriaRequest req, CancellationToken ct)
    {
        var cat = await db.CategoriasFinanceiras.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Categoria não encontrada.");
        cat.Atualizar(req.Nome, req.Cor);
        db.CategoriasFinanceiras.Update(cat);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var cat = await db.CategoriasFinanceiras.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Categoria não encontrada.");
        cat.Desativar();
        db.CategoriasFinanceiras.Update(cat);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarCategoriaRequest(Guid EmpresaId, string Nome, string Tipo, string? Cor = null);
public record AtualizarCategoriaRequest(string Nome, string? Cor = null);
