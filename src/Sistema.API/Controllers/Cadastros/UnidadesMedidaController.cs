using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

[ApiController]
[Route("api/unidades-medida")]
[Authorize]
public class UnidadesMedidaController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        // Garante que todas as unidades padrão existam para a empresa (upsert por sigla).
        // Também corrige Pesavel caso a unidade já existia antes do campo ser criado.
        var existentes = await db.UnidadesMedida
            .Where(u => u.EmpresaId == empresaId)
            .ToListAsync(ct);

        bool alterou = false;
        foreach (var (sigla, descricao, pesavel) in UnidadeMedida.Padroes)
        {
            var atual = existentes.FirstOrDefault(u => u.Sigla == sigla);
            if (atual is null)
            {
                db.UnidadesMedida.Add(UnidadeMedida.Criar(empresaId, sigla, descricao, pesavel));
                alterou = true;
            }
            else if (atual.Pesavel != pesavel)
            {
                atual.Editar(atual.Sigla, atual.Descricao, pesavel);
                alterou = true;
            }
        }

        if (alterou)
            await uow.SalvarAsync(ct);

        return Ok(await db.UnidadesMedida.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Ativo)
            .OrderBy(u => u.Sigla)
            .Select(u => new { u.Id, u.Sigla, u.Descricao, u.Pesavel })
            .ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] UnidadeMedidaRequest req, CancellationToken ct)
    {
        var um = UnidadeMedida.Criar(req.EmpresaId, req.Sigla, req.Descricao, req.Pesavel);
        db.UnidadesMedida.Add(um);
        await uow.SalvarAsync(ct);
        return Ok(new { um.Id, um.Sigla, um.Descricao, um.Pesavel });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] UnidadeMedidaRequest req, CancellationToken ct)
    {
        var um = await db.UnidadesMedida.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Unidade de medida não encontrada.");
        um.Editar(req.Sigla, req.Descricao, req.Pesavel);
        db.UnidadesMedida.Update(um);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var um = await db.UnidadesMedida.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Unidade de medida não encontrada.");
        db.UnidadesMedida.Remove(um);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record UnidadeMedidaRequest(Guid EmpresaId, string Sigla, string Descricao, bool Pesavel = false);
