using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/custos-fixos")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
public class CustosFixosController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.CustosFixos.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .OrderBy(c => c.Categoria).ThenBy(c => c.Descricao)
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarCustoFixoRequest req, CancellationToken ct)
    {
        var custo = CustoFixo.Criar(req.EmpresaId, req.Descricao, req.Valor, req.Categoria);
        db.CustosFixos.Add(custo);
        await uow.SalvarAsync(ct);
        return Ok(new { custo.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarCustoFixoRequest req, CancellationToken ct)
    {
        var custo = await db.CustosFixos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Custo fixo não encontrado.");
        custo.Atualizar(req.Descricao, req.Valor, req.Categoria);
        db.CustosFixos.Update(custo);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var custo = await db.CustosFixos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Custo fixo não encontrado.");
        custo.Desativar();
        db.CustosFixos.Update(custo);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarCustoFixoRequest(Guid EmpresaId, string Descricao, decimal Valor, string? Categoria = null);
