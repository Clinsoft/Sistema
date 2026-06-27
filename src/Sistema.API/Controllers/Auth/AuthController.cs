using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Application.Auth.Commands;

namespace Sistema.API.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>Autentica o usuário e retorna um token JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand cmd, CancellationToken ct)
    {
        try
        {
            var resultado = await mediator.Send(cmd, ct);
            return Ok(resultado);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { mensagem = ex.Message });
        }
    }

    /// <summary>Cria um novo usuário (apenas Administrador).</summary>
    [HttpPost("usuarios")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> CriarUsuario([FromBody] CriarUsuarioCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Ok(new { id });
    }
}
