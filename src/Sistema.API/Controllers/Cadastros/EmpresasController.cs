using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

[ApiController]
[Route("api/empresas")]
[Authorize]
public class EmpresasController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var e = await db.Empresas.FindAsync([id], ct);
        if (e is null) return NotFound();

        return Ok(new
        {
            e.Id, e.RazaoSocial, e.NomeFantasia, e.Cnpj,
            e.InscricaoEstadual, e.InscricaoMunicipal, e.RegimeTributario,
            e.Logradouro, e.Numero, e.Complemento, e.Bairro,
            e.Cidade, e.Uf, e.Cep, e.Telefone, e.Email, e.Ativo
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarEmpresaRequest req, CancellationToken ct)
    {
        var e = await db.Empresas.FindAsync([id], ct);
        if (e is null) return NotFound();

        e.Atualizar(req.RazaoSocial, req.NomeFantasia, req.RegimeTributario,
            req.Logradouro, req.Numero, req.Complemento, req.Bairro,
            req.Cidade, req.Uf, req.Cep, req.Telefone, req.Email,
            req.InscricaoEstadual ?? "", req.InscricaoMunicipal ?? "");

        db.Empresas.Update(e);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record AtualizarEmpresaRequest(
    string RazaoSocial, string NomeFantasia, string RegimeTributario,
    string Logradouro, string Numero, string? Complemento,
    string Bairro, string Cidade, string Uf, string Cep,
    string Telefone, string Email,
    string? InscricaoEstadual = null, string? InscricaoMunicipal = null);
