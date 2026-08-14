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
    private readonly string? _pixabayKey = config["Pixabay:ApiKey"];
    private readonly string? _unsplashKey = config["Unsplash:AccessKey"];
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

    /// <summary>Baixa os bytes de uma URL de imagem escolhida pelo usuário (foto selecionada).</summary>
    public Task<(byte[] Bytes, string Mime)?> BaixarUrlAsync(string imgUrl, CancellationToken ct = default)
        => BaixarAsync(imgUrl, ct);

    /// <summary>Retorna VÁRIAS opções de imagem para o termo (para o usuário escolher a certa).
    /// Pexels primeiro (se configurado) e completa com Openverse. Não baixa — só as URLs.</summary>
    public async Task<IReadOnlyList<CandidataImagem>> BuscarCandidatasAsync(string? termo, CancellationToken ct = default)
    {
        var q = (termo ?? "").Trim();
        if (q.Length < 2) return Array.Empty<CandidataImagem>();

        // Junta TODAS as fontes disponíveis e intercala (round-robin) para o usuário ver
        // variedade — antes uma fonte dominante escondia as outras.
        var fontes = new List<List<CandidataImagem>>();
        if (UsaPexels) fontes.Add(await CandidatasPexelsAsync(q, ct));
        if (!string.IsNullOrWhiteSpace(_pixabayKey)) fontes.Add(await CandidatasPixabayAsync(q, ct));
        if (!string.IsNullOrWhiteSpace(_unsplashKey)) fontes.Add(await CandidatasUnsplashAsync(q, ct));
        fontes.Add(await CandidatasOpenverseAsync(q, ct)); // sempre (grátis, sem chave)

        var merged = new List<CandidataImagem>();
        for (var i = 0; merged.Count < 60; i++)
        {
            var adicionou = false;
            foreach (var f in fontes)
                if (i < f.Count) { merged.Add(f[i]); adicionou = true; }
            if (!adicionou) break;
        }
        return merged;
    }

    private async Task<List<CandidataImagem>> CandidatasPixabayAsync(string q, CancellationToken ct)
    {
        var res = new List<CandidataImagem>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://pixabay.com/api/?key={_pixabayKey}&q={Uri.EscapeDataString(q)}&image_type=photo&per_page=20&lang=pt&safesearch=true");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return res;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("hits", out var hits)) return res;
            foreach (var h in hits.EnumerateArray())
            {
                var url = (h.TryGetProperty("largeImageURL", out var lg) ? lg.GetString() : null)
                       ?? (h.TryGetProperty("webformatURL", out var wf) ? wf.GetString() : null);
                var thumb = (h.TryGetProperty("webformatURL", out var wf2) ? wf2.GetString() : null)
                         ?? (h.TryGetProperty("previewURL", out var pv) ? pv.GetString() : null) ?? url;
                var autor = h.TryGetProperty("user", out var us) ? us.GetString() : null;
                if (!string.IsNullOrWhiteSpace(url))
                    res.Add(new CandidataImagem(url!, thumb ?? url!, "Pixabay", autor));
            }
        }
        catch { /* ignora */ }
        return res;
    }

    private async Task<List<CandidataImagem>> CandidatasUnsplashAsync(string q, CancellationToken ct)
    {
        var res = new List<CandidataImagem>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(q)}&per_page=20&content_filter=high");
            req.Headers.TryAddWithoutValidation("Authorization", $"Client-ID {_unsplashKey}");
            req.Headers.Accept.ParseAdd("application/json");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return res;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("results", out var results)) return res;
            foreach (var r in results.EnumerateArray())
            {
                if (!r.TryGetProperty("urls", out var urls)) continue;
                var url = (urls.TryGetProperty("regular", out var rg) ? rg.GetString() : null)
                       ?? (urls.TryGetProperty("small", out var sm) ? sm.GetString() : null);
                var thumb = (urls.TryGetProperty("small", out var sm2) ? sm2.GetString() : null)
                         ?? (urls.TryGetProperty("thumb", out var tb) ? tb.GetString() : null) ?? url;
                string? autor = r.TryGetProperty("user", out var user) && user.TryGetProperty("name", out var nm)
                    ? nm.GetString() : null;
                if (!string.IsNullOrWhiteSpace(url))
                    res.Add(new CandidataImagem(url!, thumb ?? url!, "Unsplash", autor));
            }
        }
        catch { /* ignora */ }
        return res;
    }

    private async Task<List<CandidataImagem>> CandidatasPexelsAsync(string q, CancellationToken ct)
    {
        var res = new List<CandidataImagem>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(q)}&per_page=15&locale=pt-BR");
            req.Headers.TryAddWithoutValidation("Authorization", _pexelsKey);
            req.Headers.Accept.ParseAdd("application/json");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return res;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("photos", out var photos)) return res;
            foreach (var ph in photos.EnumerateArray())
            {
                if (!ph.TryGetProperty("src", out var src)) continue;
                var url = (src.TryGetProperty("large", out var lg) ? lg.GetString() : null)
                       ?? (src.TryGetProperty("medium", out var md) ? md.GetString() : null);
                var thumb = (src.TryGetProperty("medium", out var m2) ? m2.GetString() : null)
                         ?? (src.TryGetProperty("small", out var sm) ? sm.GetString() : null) ?? url;
                var autor = ph.TryGetProperty("photographer", out var pg) ? pg.GetString() : null;
                if (!string.IsNullOrWhiteSpace(url))
                    res.Add(new CandidataImagem(url!, thumb ?? url!, "Pexels", autor));
            }
        }
        catch { /* ignora */ }
        return res;
    }

    private async Task<List<CandidataImagem>> CandidatasOpenverseAsync(string q, CancellationToken ct)
    {
        var res = new List<CandidataImagem>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.openverse.org/v1/images/?q={Uri.EscapeDataString(q)}&license=cc0,pdm&mature=false&page_size=15");
            req.Headers.UserAgent.ParseAdd("EcoGranel/1.0 (+https://ecogranel.com.br)");
            req.Headers.Accept.ParseAdd("application/json");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return res;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("results", out var results)) return res;
            foreach (var r in results.EnumerateArray())
            {
                var url = r.TryGetProperty("url", out var u) ? u.GetString() : null;
                var thumb = (r.TryGetProperty("thumbnail", out var th) ? th.GetString() : null) ?? url;
                var autor = r.TryGetProperty("creator", out var cr) ? cr.GetString() : null;
                if (!string.IsNullOrWhiteSpace(thumb))
                    res.Add(new CandidataImagem(url ?? thumb!, thumb!, "Openverse", autor));
            }
        }
        catch { /* ignora */ }
        return res;
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

/// <summary>Uma opção de imagem para o usuário escolher (não baixada ainda).</summary>
public record CandidataImagem(string Url, string Thumb, string Fonte, string? Autor);
