using Sistema.Domain.Cadastros.Entities;

namespace Sistema.Domain.Auth;

public interface IJwtTokenService
{
    string GerarToken(Usuario usuario);
    DateTime Expiracao { get; }
}
