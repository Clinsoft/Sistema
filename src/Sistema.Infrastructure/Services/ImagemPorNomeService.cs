using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Busca imagem de produto pela SEMELHANÇA DO NOME (para granel/KG, que não têm EAN).
/// Fonte: Openverse (agrega imagens de licença aberta — Flickr, Wikimedia etc.),
/// restrito a CC0 + Domínio Público (uso comercial, sem exigência de atribuição).
/// Retorna (bytes, mime) ou null quando não há resultado. Lança em erro de rede/limite
/// (o chamador para o lote e NÃO marca o produto como tentado).
/// </summary>
public class ImagemPorNomeService(HttpClient http, IConfiguration config)
{
    private const string OpenverseUrl =
        "https://api.openverse.org/v1/images/?q={0}&license=cc0,pdm&mature=false&page_size=3";
    private const string PexelsUrl =
        "https://api.pexels.com/v1/search?query={0}&per_page=3&locale=pt-BR";

    private readonly string? _pexelsKey = config["Pexels:ApiKey"];
    public bool UsaPexels => !string.IsNullOrWhiteSpace(_pexelsKey);

    // Palavras que não ajudam na busca (unidades, embalagem, conectivos).
    private static readonly HashSet<string> Ruido = new(StringComparer.OrdinalIgnoreCase)
    {
        "KG","KGS","G","GR","GRS","GRAMA","GRAMAS","MG","ML","L","LT","LTS","LITRO","LITROS",
        "UN","UND","UNID","UNIDADE","PCT","PCTE","PACOTE","PC","CX","CAIXA","FD","DP","BD","SC",
        "GRANEL","FRACIONADO","APROX","EMBALAGEM","EMB","APROXIMADO",
        "DE","DA","DO","DAS","DOS","COM","SEM","E","OU","POR","P","C","S","A","O",
    };

    public async Task<(byte[] Bytes, string Mime)?> BuscarPorNomeAsync(string? nome, CancellationToken ct = default)
    {
        var kw = PalavraChave(nome);
        if (kw.Length < 3) return null; // nada pesquisável → resposta definitiva "não achou"

        if (UsaPexels)
        {
            var viaPexels = await BuscarPexelsAsync(kw, ct);
            if (viaPexels is not null) return viaPexels;
        }
        return await BuscarOpenverseAsync(kw, ct);
    }

    // ── Pexels (fotos grátis, uso comercial, sem atribuição) ──────────────────
    private async Task<(byte[], string)?> BuscarPexelsAsync(string kw, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, string.Format(PexelsUrl, Uri.EscapeDataString(kw)));
        req.Headers.TryAddWithoutValidation("Authorization", _pexelsKey);
        req.Headers.Accept.ParseAdd("application/json");

        using var resp = await http.SendAsync(req, ct);
        if ((int)resp.StatusCode == 429) throw new HttpRequestException("Pexels rate limit");
        if (!resp.IsSuccessStatusCode) return null; // chave inválida/etc → cai no Openverse

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("photos", out var photos) || photos.GetArrayLength() == 0)
            return null;

        foreach (var ph in photos.EnumerateArray())
        {
            if (!ph.TryGetProperty("src", out var src)) continue;
            var img = (src.TryGetProperty("large", out var lg) ? lg.GetString() : null)
                   ?? (src.TryGetProperty("medium", out var md) ? md.GetString() : null);
            if (string.IsNullOrWhiteSpace(img)) continue;

            var baixado = await BaixarAsync(img, ct);
            if (baixado is not null) return baixado;
        }
        return null;
    }

    // ── Openverse (licença livre CC0/domínio público) ─────────────────────────
    private async Task<(byte[], string)?> BuscarOpenverseAsync(string kw, CancellationToken ct)
    {
        var url = string.Format(OpenverseUrl, Uri.EscapeDataString(kw));
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("EcoGranel/1.0 (+https://ecogranel.com.br)");
        req.Headers.Accept.ParseAdd("application/json");

        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode(); // 429/5xx → exceção (chamador para o lote)

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return null;

        foreach (var r in results.EnumerateArray())
        {
            var img = (r.TryGetProperty("thumbnail", out var th) ? th.GetString() : null)
                   ?? (r.TryGetProperty("url", out var u) ? u.GetString() : null);
            if (string.IsNullOrWhiteSpace(img)) continue;

            var baixado = await BaixarAsync(img, ct);
            if (baixado is not null) return baixado;
        }
        return null;
    }

    private async Task<(byte[], string)?> BaixarAsync(string imgUrl, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, imgUrl);
            req.Headers.UserAgent.ParseAdd("EcoGranel/1.0 (+https://ecogranel.com.br)");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var mime = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (bytes.Length < 500) return null; // provável placeholder/erro
            return (bytes, mime);
        }
        catch { return null; }
    }

    /// <summary>Reduz o nome do produto a uma palavra-chave de busca: sem acento, sem
    /// números/unidades/ruído, mantendo as ~3 primeiras palavras significativas.
    /// Ex.: "ARROZ INTEGRAL 1KG GRANEL" → "arroz integral".</summary>
    public static string PalavraChave(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";
        var semAcento = new string(nome
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());

        var tokens = semAcento
            .Split(new[] { ' ', '/', ',', '.', ';', ':', '-', '(', ')', '+', '*', '\t' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .Where(t => !t.Any(char.IsDigit))         // tira "500g", "1kg", números
            .Where(t => !Ruido.Contains(t))
            .Take(3)
            .Select(t => t.ToLowerInvariant());

        return string.Join(' ', tokens).Trim();
    }
}
