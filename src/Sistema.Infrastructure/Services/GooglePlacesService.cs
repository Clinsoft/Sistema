using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Busca de concorrentes via Google Places API (New) — Text Search. Diferente do OSM,
/// o Google TEM as lojas de produtos naturais mapeadas. Requer chave em
/// Google:PlacesApiKey (com Places API habilitada e billing ativo no Google Cloud).
/// </summary>
public class GooglePlacesService(HttpClient http, IConfiguration config)
{
    private const string Url = "https://places.googleapis.com/v1/places:searchText";

    public bool Configurado => !string.IsNullOrWhiteSpace(config["Google:PlacesApiKey"]);

    // Consultas do segmento (cada uma é 1 Text Search). Mescladas e dedup por place id.
    private static readonly string[] Consultas =
    {
        "loja de produtos naturais", "produtos naturais a granel", "suplementos alimentares", "empório saudável",
    };

    public async Task<List<MapaConcorrenciaService.ConcorrenteOsm>> BuscarAsync(
        double lat, double lng, int raioMetros, CancellationToken ct = default)
    {
        var key = config["Google:PlacesApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Google Places não configurado (Google:PlacesApiKey).");

        var porId = new Dictionary<string, MapaConcorrenciaService.ConcorrenteOsm>();
        foreach (var q in Consultas)
        {
            var lista = await ConsultarAsync(key!, q, lat, lng, raioMetros, ct);
            foreach (var c in lista) porId[c.OsmRef] = c;   // dedup por place id
            await Task.Delay(200, ct);
        }

        // O locationBias não é um limite rígido — filtra pelo raio real (haversine).
        return porId.Values
            .Where(c => (double)c.DistanciaKm * 1000 <= raioMetros + 50)
            .OrderBy(c => c.DistanciaKm)
            .ToList();
    }

    private async Task<List<MapaConcorrenciaService.ConcorrenteOsm>> ConsultarAsync(
        string key, string textQuery, double lat, double lng, int raioMetros, CancellationToken ct)
    {
        var body = new
        {
            textQuery,
            languageCode = "pt-BR",
            regionCode = "BR",
            maxResultCount = 20,
            locationBias = new { circle = new { center = new { latitude = lat, longitude = lng }, radius = (double)raioMetros } },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Goog-Api-Key", key);
        req.Headers.Add("X-Goog-FieldMask",
            "places.id,places.displayName,places.formattedAddress,places.location,places.primaryTypeDisplayName,places.nationalPhoneNumber,places.websiteUri");

        using var resp = await http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erro do Google Places ({(int)resp.StatusCode}): {ExtrairErro(raw)}");

        var resultado = new List<MapaConcorrenciaService.ConcorrenteOsm>();
        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("places", out var places)) return resultado;

        foreach (var p in places.EnumerateArray())
        {
            var id = p.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrEmpty(id)) continue;
            var nome = p.TryGetProperty("displayName", out var dn) && dn.TryGetProperty("text", out var dt) ? dt.GetString() : null;
            if (string.IsNullOrWhiteSpace(nome)) continue;
            if (!p.TryGetProperty("location", out var loc)
                || !loc.TryGetProperty("latitude", out var plaEl) || !loc.TryGetProperty("longitude", out var ploEl))
                continue;

            var pla = plaEl.GetDouble(); var plo = ploEl.GetDouble();
            var categoria = p.TryGetProperty("primaryTypeDisplayName", out var pt) && pt.TryGetProperty("text", out var ptt)
                ? ptt.GetString() : "Produtos naturais";
            var endereco = p.TryGetProperty("formattedAddress", out var fa) ? fa.GetString() : null;
            var telefone = p.TryGetProperty("nationalPhoneNumber", out var np) ? np.GetString() : null;
            var website = p.TryGetProperty("websiteUri", out var ws) ? ws.GetString() : null;
            var dist = (decimal)Math.Round(MapaConcorrenciaService.DistanciaKm(lat, lng, pla, plo), 2);

            resultado.Add(new MapaConcorrenciaService.ConcorrenteOsm(
                id!, nome!, categoria, pla, plo, endereco, telefone, website, dist));
        }
        return resultado;
    }

    private static string ExtrairErro(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? body;
        }
        catch { /* ignora */ }
        return body.Length > 300 ? body[..300] : body;
    }
}
