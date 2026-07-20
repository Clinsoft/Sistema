using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Cadastros.Commands;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

[ApiController]
[Route("api/fornecedores")]
[Authorize]
public class FornecedoresController(IMediator mediator, IFornecedorRepository repo,
    SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId, [FromQuery] string? q, [FromQuery] string? termo,
        [FromQuery] bool? ativo, CancellationToken ct = default)
    {
        var busca = q ?? termo;
        var query = db.Fornecedores.AsNoTracking().Where(f => f.EmpresaId == empresaId);
        if (ativo.HasValue) query = query.Where(f => f.Ativo == ativo.Value);
        if (!string.IsNullOrWhiteSpace(busca))
            query = query.Where(f => f.RazaoSocial.Contains(busca)
                || (f.NomeFantasia != null && f.NomeFantasia.Contains(busca))
                || (f.Cnpj != null && f.Cnpj.Contains(busca)));

        var fornecedores = await query.OrderBy(f => f.RazaoSocial).ToListAsync(ct);

        var docsClientes = (await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.CpfCnpj != null)
            .Select(c => c.CpfCnpj!).ToListAsync(ct)).ToHashSet();

        return Ok(fornecedores.Select(f => new
        {
            f.Id, f.RazaoSocial, f.NomeFantasia, f.Cnpj,
            f.Email, f.Telefone, f.Celular, f.Contato, f.Cidade, f.Uf,
            tipos = string.IsNullOrEmpty(f.Tipos) ? new[] { "Fornecedor" } : f.Tipos.Split(','),
            ehCliente = f.Cnpj != null && docsClientes.Contains(f.Cnpj),
            f.PrazoPagamentoDias, f.Ativo
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var f = await db.Fornecedores.FindAsync([id], ct);
        if (f is null) return NotFound();
        return Ok(new
        {
            f.Id, f.EmpresaId, f.RazaoSocial, f.NomeFantasia, f.Cnpj,
            f.InscricaoEstadual, f.Email, f.Telefone, f.Celular, f.Contato,
            tipos = string.IsNullOrEmpty(f.Tipos) ? new[] { "Fornecedor" } : f.Tipos.Split(','),
            f.Logradouro, f.Numero, f.Complemento, f.Bairro, f.Cidade, f.Uf, f.Cep,
            f.PrazoPagamentoDias, f.Observacao, f.Ativo
        });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarFornecedorCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] EditarFornecedorRequest req, CancellationToken ct)
    {
        var f = await db.Fornecedores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Fornecedor não encontrado.");

        f.Editar(req.RazaoSocial, req.NomeFantasia, req.Email, req.Telefone, req.Contato,
            req.PrazoPagamentoDias, req.Logradouro, req.Numero, req.Complemento,
            req.Bairro, req.Cidade, req.Uf, req.Cep, req.InscricaoEstadual, req.Observacao,
            req.Celular, req.Tipos);

        db.Fornecedores.Update(f);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var f = await db.Fornecedores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Fornecedor não encontrado.");
        f.Desativar();
        db.Fornecedores.Update(f);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        var f = await db.Fornecedores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Fornecedor não encontrado.");
        f.Reativar();
        db.Fornecedores.Update(f);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record EditarFornecedorRequest(
    string RazaoSocial, string? NomeFantasia, string? Email,
    string? Telefone, string? Contato, int PrazoPagamentoDias,
    string? Logradouro = null, string? Numero = null, string? Complemento = null,
    string? Bairro = null, string? Cidade = null, string? Uf = null,
    string? Cep = null, string? InscricaoEstadual = null, string? Observacao = null,
    string? Celular = null, string? Tipos = null);
