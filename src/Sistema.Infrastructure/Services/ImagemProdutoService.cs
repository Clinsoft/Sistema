using System.Text.Json;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Busca a imagem de um produto pelo código de barras (EAN/GTIN) na base pública
/// Open Food Facts. Retorna os bytes da imagem e o mime-type, ou null se não achar.
/// </summary>
public class ImagemProdutoService(HttpClient http)
{
    // API v2 da Open Food Facts. Pede só os campos de imagem para resposta enxuta.
    private const string ApiUrl =
        "https://world.openfoodfacts.org/api/v2/product/{0}.json?fields=product_name,image_url,image_front_url";

    /// <summary>
    /// Procura a imagem do produto pelo código de barras. Retorna (bytes, mime, nome)
    /// ou null se não houver produto/imagem para o código.
    /// </summary>
    public async Task<(byte[] Bytes, string Mime, string? Nome)?> BuscarAsync(
        string codigoBarras, CancellationToken ct = default)
    {
        var ean = new string((codigoBarras ?? "").Where(char.IsDigit).ToArray());
        if (ean.Length < 8) return null; // EAN-8 mínimo

        var url = string.Format(ApiUrl, ean);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        // A OFF exige um User-Agent identificável.
        req.Headers.UserAgent.ParseAdd("EcoGranel/1.0 (+https://ecogranel.com.br)");

        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("status", out var st) || st.GetInt32() != 1) return null;
        if (!root.TryGetProperty("product", out var prod)) return null;

        string? imgUrl =
            (prod.TryGetProperty("image_front_url", out var f) ? f.GetString() : null)
            ?? (prod.TryGetProperty("image_url", out var i) ? i.GetString() : null);
        if (string.IsNullOrWhiteSpace(imgUrl)) return null;

        var nome = prod.TryGetProperty("product_name", out var n) ? n.GetString() : null;

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
