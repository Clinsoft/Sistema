using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Auth.Commands;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Auth;

[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "Administrador")]
public class UsuariosController(SistemaDbContext db, IUsuarioRepository repo, IUnitOfWork uow, IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId)
            .Select(u => new { u.Id, u.Nome, u.Email, u.Perfil, u.Ativo, u.UltimoAcesso })
            .OrderBy(u => u.Nome)
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarUsuarioRequest req, CancellationToken ct)
    {
        var id = await mediator.Send(new CriarUsuarioCommand(req.EmpresaId, req.Nome, req.Email, req.Senha, req.Perfil), ct);
        return CreatedAtAction(nameof(Listar), new { id });
    }

    [HttpPatch("{id:guid}/perfil")]
    public async Task<IActionResult> AlterarPerfil(Guid id, [FromBody] AlterarPerfilRequest req, CancellationToken ct)
    {
        var usuario = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");
        usuario.AlterarPerfil(req.Perfil);
        repo.Atualizar(usuario);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var usuario = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");
        usuario.Desativar();
        repo.Atualizar(usuario);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        var usuario = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");
        usuario.Reativar();
        repo.Atualizar(usuario);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/senha")]
    public async Task<IActionResult> AlterarSenha(Guid id, [FromBody] AlterarSenhaRequest req, CancellationToken ct)
    {
        var usuario = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");
        usuario.AlterarSenha(BCrypt.Net.BCrypt.HashPassword(req.NovaSenha));
        repo.Atualizar(usuario);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarUsuarioRequest(Guid EmpresaId, string Nome, string Email, string Senha, string Perfil);
public record AlterarPerfilRequest(string Perfil);
public record AlterarSenhaRequest(string NovaSenha);
