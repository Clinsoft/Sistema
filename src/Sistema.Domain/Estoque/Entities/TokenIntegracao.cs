using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Token OAuth de uma integração externa (ex.: Mercado Livre). Guarda access/refresh
/// token e a expiração para renovar automaticamente. Uma linha por provedor.
/// </summary>
public class TokenIntegracao : Entity
{
    public string Provedor { get; private set; } = null!;   // "MercadoLivre"
    public string AccessToken { get; private set; } = null!;
    public string? RefreshToken { get; private set; }
    public DateTime ExpiraEm { get; private set; }           // UTC
    public string? UsuarioExterno { get; private set; }      // id do usuário no provedor

    private TokenIntegracao() { }

    public static TokenIntegracao Criar(string provedor, string accessToken, string? refreshToken,
        DateTime expiraEmUtc, string? usuarioExterno)
        => new()
        {
            Provedor = provedor, AccessToken = accessToken, RefreshToken = refreshToken,
            ExpiraEm = expiraEmUtc, UsuarioExterno = usuarioExterno,
        };

    public void Atualizar(string accessToken, string? refreshToken, DateTime expiraEmUtc)
    {
        AccessToken = accessToken;
        if (!string.IsNullOrWhiteSpace(refreshToken)) RefreshToken = refreshToken;
        ExpiraEm = expiraEmUtc;
    }

    /// <summary>Expirado (com folga de 5 min) — precisa renovar antes de usar.</summary>
    public bool Expirado => DateTime.UtcNow >= ExpiraEm.AddMinutes(-5);
}
