using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Busca a imagem de um produto pelo código de barras (EAN/GTIN).
/// Fonte primária: Cosmos/Bluesoft (base BR, exige token de assinatura em
/// Cosmos:ApiToken — as imagens são de uso do assinante). Fallback: Open Food
/// Facts (base pública/aberta). Retorna (bytes, mime, nome) ou null se não achar.
/// </summary>
public class ImagemProdutoService(HttpClient http, IConfiguration config)
{
    private const string OffUrl =
        "https://world.openfoodfacts.org/api/v2/product/{0}.json?fields=product_name,image_url,image_front_url";
    private const string CosmosUrl = "https://api.cosmos.bluesoft.com.br/gtins/{0}.json";

    private readonly string? _cosmosToken = config["Cosmos:ApiToken"];

    /// <summary>Há token do Cosmos configurado? (o lote usa isso p/ ajustar o ritmo das chamadas).</summary>
    public bool UsaCosmos => !string.IsNullOrWhiteSpace(_cosmosToken);

    public async Task<(byte[] Bytes, string Mime, string? Nome)?> BuscarAsync(
        string codigoBarras, CancellationToken ct = default)
    {
        var ean = new string((codigoBarras ?? "").Where(char.IsDigit).ToArray());
        if (ean.Length < 8) return null; // EAN-8 mínimo

        if (UsaCosmos)
        {
            var viaCosmos = await BuscarCosmosAsync(ean, ct);
            if (viaCosmos is not null) return viaCosmos;
        }
        return await BuscarOpenFoodFactsAsync(ean, ct);
    }

    // ── Cosmos / Bluesoft (base BR, boa cobertura de imagem) ──────────────────
    private async Task<(byte[], string, string?)?> BuscarCosmosAsync(string ean, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, string.Format(CosmosUrl, ean));
            req.Headers.TryAddWithoutValidation("X-Cosmos-Token", _cosmosToken);
            req.Headers.UserAgent.ParseAdd("EcoGranel/1.0 (+https://ecogranel.com.br)");
            req.Headers.Accept.ParseAdd("application/json");

            using var resp = await http.SendAsync(req, ct);
            // 404 = não achou; 429 = limite de requisições — em ambos, só ignora (cai no OFF).
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var imgUrl = root.TryGetProperty("thumbnail", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(imgUrl)) return null;

            var nome = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            return await BaixarAsync(imgUrl, nome, ct);
        }
        catch { return null; }
    }

    // ── Open Food Facts (aberta) ──────────────────────────────────────────────
    private async Task<(byte[], string, string?)?> BuscarOpenFoodFactsAsync(string ean, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, string.Format(OffUrl, ean));
            req.Headers.UserAgent.ParseAdd("EcoGranel/1.0 (+https://ecogranel.com.br)");

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("status", out var st) || st.GetInt32() != 1) return null;
            if (!root.TryGetProperty("product", out var prod)) return null;

            var imgUrl =
                (prod.TryGetProperty("image_front_url", out var f) ? f.GetString() : null)
                ?? (prod.TryGetProperty("image_url", out var i) ? i.GetString() : null);
            if (string.IsNullOrWhiteSpace(imgUrl)) return null;

            var nome = prod.TryGetProperty("product_name", out var n) ? n.GetString() : null;
            return await BaixarAsync(imgUrl, nome, ct);
        }
        catch { return null; }
    }

    // ── Baixa os bytes da imagem apontada pela fonte ──────────────────────────
    private async Task<(byte[], string, string?)?> BaixarAsync(string imgUrl, string? nome, CancellationToken ct)
    {
        using var imgReq = new HttpRequestMessage(HttpMethod.Get, imgUrl);
        imgReq.Headers.UserAgent.ParseAdd("EcoGranel/1.0 (+https://ecogranel.com.br)");
        using var imgResp = await http.SendAsync(imgReq, ct);
        if (!imgResp.IsSuccessStatusCode) return null;

        var bytes = await imgResp.Content.ReadAsByteArrayAsync(ct);
        var mime = imgResp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        if (bytes.Length == 0) return null;

        return (bytes, mime, nome);
    }
}
