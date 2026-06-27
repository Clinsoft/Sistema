using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Contabilidade.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Contabilidade;

[ApiController]
[Route("api/contabilidade/contadores")]
[Authorize]
public class ContadoresController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lista = await db.Contadores.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId)
            .OrderBy(c => c.Nome)
            .Select(c => new { c.Id, c.Nome, c.CpfCnpj, c.Email, c.Telefone, c.CRC, c.Ativo, c.CriadoEm })
            .ToListAsync(ct);
        return Ok(lista);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] ContadorRequest req, CancellationToken ct)
    {
        var cpfCnpjLimpo = req.CpfCnpj.Replace(".", "").Replace("-", "").Replace("/", "");

        if (await db.Contadores.AnyAsync(c => c.EmpresaId == req.EmpresaId && c.CpfCnpj == cpfCnpjLimpo, ct))
            return BadRequest("Já existe um contador cadastrado com este CPF/CNPJ.");

        var contador = Contador.Criar(req.EmpresaId, req.Nome, req.CpfCnpj, req.Email, req.Telefone, req.CRC);
        db.Contadores.Add(contador);
        await uow.SalvarAsync(ct);
        return Ok(new { contador.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] ContadorEditarRequest req, CancellationToken ct)
    {
        var contador = await db.Contadores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        contador.Editar(req.Nome, req.Email, req.Telefone, req.CRC);
        db.Contadores.Update(contador);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var contador = await db.Contadores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        contador.Desativar();
        db.Contadores.Update(contador);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        var contador = await db.Contadores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        contador.Reativar();
        db.Contadores.Update(contador);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record ContadorRequest(Guid EmpresaId, string Nome, string CpfCnpj, string Email,
    string? Telefone = null, string? CRC = null);
public record ContadorEditarRequest(string Nome, string Email, string? Telefone, string? CRC);
