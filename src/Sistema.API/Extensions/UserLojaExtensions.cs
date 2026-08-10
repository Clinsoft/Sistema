using System.Security.Claims;

namespace Sistema.API.Extensions;

/// <summary>Helpers para restringir o atendente à própria loja (loja de origem no login/JWT).</summary>
public static class UserLojaExtensions
{
    public static bool EhAtendente(this ClaimsPrincipal user) => user.IsInRole("Atendente");

    /// <summary>Loja (LocalEstoqueId) do usuário logado, vinda do claim do JWT.</summary>
    public static Guid? LojaClaim(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirst("localEstoqueId")?.Value, out var id) ? id : null;

    /// <summary>
    /// Escopo de loja para consultas: se o usuário é atendente, força a loja dele
    /// (ignora o valor recebido do front); senão mantém o que veio.
    /// </summary>
    public static Guid? EscoparLoja(this ClaimsPrincipal user, Guid? recebido)
        => user.EhAtendente() ? (user.LojaClaim() ?? Guid.Empty) : recebido;

    /// <summary>Versão para parâmetros não-nulos (Guid): atendente sempre na própria loja.</summary>
    public static Guid EscoparLoja(this ClaimsPrincipal user, Guid recebido)
        => user.EhAtendente() ? (user.LojaClaim() ?? Guid.Empty) : recebido;
}
