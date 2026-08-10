using System.Security.Claims;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.API.Auth;

/// <summary>Usuário atual a partir do JWT/HttpContext (para o log de auditoria).</summary>
public class CurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    private ClaimsPrincipal? User => http.HttpContext?.User;

    public Guid? UsuarioId =>
        Guid.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Nome => User?.FindFirst("nome")?.Value;

    public Guid? EmpresaId =>
        Guid.TryParse(User?.FindFirst("empresaId")?.Value, out var id) ? id : null;

    public string? Ip => http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
