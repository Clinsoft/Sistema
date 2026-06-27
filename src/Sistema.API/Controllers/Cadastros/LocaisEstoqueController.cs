using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

[ApiController]
[Route("api/locais-estoque")]
[Authorize]
public class LocaisEstoqueController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Ativo)
            .OrderByDescending(l => l.Principal).ThenBy(l => l.Nome)
            .Select(l => new { l.Id, l.Nome, l.Descricao, l.Principal })
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] LocalEstoqueRequest req, CancellationToken ct)
    {
        // Só pode haver um local principal por empresa
        if (req.Principal)
        {
            await db.LocaisEstoque
                .Where(l => l.EmpresaId == req.EmpresaId && l.Principal && l.Ativo)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.Principal, false), ct);
        }

        var local = LocalEstoque.Criar(req.EmpresaId, req.Nome, req.Principal, req.Descricao);
        db.LocaisEstoque.Add(local);
        await uow.SalvarAsync(ct);
        return Ok(new { local.Id, local.Nome, local.Principal });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] LocalEstoqueRequest req, CancellationToken ct)
    {
        var local = await db.LocaisEstoque.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Local de estoque não encontrado.");

        if (req.Principal && !local.Principal)
        {
            await db.LocaisEstoque
                .Where(l => l.EmpresaId == local.EmpresaId && l.Principal && l.Ativo)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.Principal, false), ct);
        }

        local.Editar(req.Nome, req.Principal, req.Descricao);
        db.LocaisEstoque.Update(local);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var local = await db.LocaisEstoque.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Local de estoque não encontrado.");
        if (local.Principal)
            return BadRequest("Não é possível remover o local de estoque principal.");
        db.LocaisEstoque.Remove(local);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record LocalEstoqueRequest(Guid EmpresaId, string Nome, bool Principal = false, string? Descricao = null);
