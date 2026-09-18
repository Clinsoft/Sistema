using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sistema.Domain.Estoque.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Integração com o Mercado Livre (preço de mercado online por nome/EAN).
/// OAuth 2.0 authorization_code: o usuário autoriza o app uma vez; guardamos
/// access/refresh token e renovamos automaticamente (token expira ~6h).
/// Config: MercadoLivre:ClientId, MercadoLivre:ClientSecret, MercadoLivre:RedirectUri.
/// </summary>
public class MercadoLivreService(HttpClient http, IConfiguration config, SistemaDbContext db)
{
    private const string Provedor = "MercadoLivre";
    private const string AuthBase = "https://auth.mercadolivre.com.br/authorization";
    private const string TokenUrl = "https://api.mercadolibre.com/oauth/token";
    private const string SearchUrl = "https://api.mercadolibre.com/sites/MLB/search";

    private string? ClientId => config["MercadoLivre:ClientId"];
    private string? ClientSecret => config["MercadoLivre:ClientSecret"];
    private string RedirectUri => config["MercadoLivre:RedirectUri"]
        ?? "https://sistema.ecogranel.com.br/api/mercadolivre/callback";

    public bool Configurado => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public async Task<bool> ConectadoAsync(CancellationToken ct = default)
        => await db.TokensIntegracao.AnyAsync(t => t.Provedor == Provedor, ct);

    // PKCE: o ML exige code_challenge na autorização e code_verifier na troca do code.
    // Guarda o verifier por state (em memória; /autorizar e /callback caem na mesma instância ativa).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string verifier, DateTime criado)> _pkce = new();

    public string UrlAutorizacao()
    {
        var verifier = Base64Url(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N");
        var limite = DateTime.UtcNow.AddMinutes(-15);
        foreach (var kv in _pkce) if (kv.Value.criado < limite) _pkce.TryRemove(kv.Key, out _);
        _pkce[state] = (verifier, DateTime.UtcNow);
        return $"{AuthBase}?response_type=code&client_id={Uri.EscapeDataString(ClientId!)}" +
               $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
               $"&code_challenge={challenge}&code_challenge_method=S256&state={state}";
    }

    private static string Base64Url(byte[] b)
        => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public record ConfiguracaoInfo(bool Configurado, bool Conectado, string RedirectUri);

    public record ItemMl(string Titulo, decimal Preco, string? Permalink);

    /// <summary>Troca o code (do callback) por access/refresh token e salva. Usa o code_verifier (PKCE).</summary>
    public async Task TrocarCodigoAsync(string code, string? state, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId!,
            ["client_secret"] = ClientSecret!,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
        };
        if (!string.IsNullOrEmpty(state) && _pkce.TryRemove(state, out var v))
            form["code_verifier"] = v.verifier;
        await SalvarTokenAsync(form, ct);
    }

    private async Task<string?> ObterAccessTokenAsync(CancellationToken ct)
    {
        var token = await db.TokensIntegracao.FirstOrDefaultAsync(t => t.Provedor == Provedor, ct);
        if (token is null) return null;
        if (!token.Expirado) return token.AccessToken;
        if (string.IsNullOrWhiteSpace(token.RefreshToken)) return token.AccessToken;

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId!,
            ["client_secret"] = ClientSecret!,
            ["refresh_token"] = token.RefreshToken!,
        };
        return await SalvarTokenAsync(form, ct);
    }

    private async Task<string?> SalvarTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = new FormUrlEncodedContent(form) };
        req.Headers.Accept.ParseAdd("application/json");
        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erro OAuth Mercado Livre ({(int)resp.StatusCode}): {body[..Math.Min(300, body.Length)]}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var access = root.GetProperty("access_token").GetString()!;
        var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 21600;
        var userId = root.TryGetProperty("user_id", out var u) ? u.ToString() : null;
        var expiraEm = DateTime.UtcNow.AddSeconds(expiresIn);

        var token = await db.TokensIntegracao.FirstOrDefaultAsync(t => t.Provedor == Provedor, ct);
        if (token is null)
            db.TokensIntegracao.Add(TokenIntegracao.Criar(Provedor, access, refresh, expiraEm, userId));
        else
            token.Atualizar(access, refresh, expiraEm);
        await db.SaveChangesAsync(ct);
        return access;
    }

    /// <summary>Busca no Mercado Livre por termo (nome/EAN). Retorna preços de anúncios.</summary>
    public async Task<List<ItemMl>> BuscarAsync(string termo, int limite = 6, CancellationToken ct = default)
    {
        var access = await ObterAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(access)) return new();

        var url = $"{SearchUrl}?q={Uri.EscapeDataString(termo)}&limit={limite}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return new();
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var lista = new List<ItemMl>();
        if (!doc.RootElement.TryGetProperty("results", out var results)) return lista;
        foreach (var it in results.EnumerateArray())
        {
            var titulo = it.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(titulo)) continue;
            if (!it.TryGetProperty("price", out var p) || p.ValueKind != JsonValueKind.Number) continue;
            var permalink = it.TryGetProperty("permalink", out var pl) ? pl.GetString() : null;
            lista.Add(new ItemMl(titulo!, p.GetDecimal(), permalink));
        }
        return lista;
    }
}
