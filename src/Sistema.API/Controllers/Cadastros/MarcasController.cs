using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

[ApiController]
[Route("api/marcas")]
[Authorize]
public class MarcasController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.Marcas.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Ativo)
            .OrderBy(m => m.Nome)
            .Select(m => new { m.Id, m.Nome })
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] MarcaRequest req, CancellationToken ct)
    {
        var marca = Marca.Criar(req.EmpresaId, req.Nome);
        db.Marcas.Add(marca);
        await uow.SalvarAsync(ct);
        return Ok(new { marca.Id, marca.Nome });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] MarcaRequest req, CancellationToken ct)
    {
        var rows = await db.Marcas
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Nome, req.Nome), ct);
        return rows > 0 ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var marca = await db.Marcas.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Marca não encontrada.");
        db.Marcas.Remove(marca);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record MarcaRequest(Guid EmpresaId, string Nome);
