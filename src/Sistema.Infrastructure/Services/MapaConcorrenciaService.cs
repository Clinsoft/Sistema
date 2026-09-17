using System.Globalization;
using System.Text.Json;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Geolocalização e busca de concorrentes via OpenStreetMap (gratuito):
/// - Geocodificação de endereço pelo Nominatim.
/// - Busca de estabelecimentos num raio pela Overpass API.
/// Respeita as políticas de uso do OSM (User-Agent identificável; poucas requisições).
/// </summary>
public class MapaConcorrenciaService(HttpClient http)
{
    private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
    private const string OverpassUrl = "https://overpass-api.de/api/interpreter";

    // shop OSM → rótulo amigável. A ordem/valores definem também o filtro da busca.
    private static readonly Dictionary<string, string> ShopLabels = new()
    {
        ["health_food"] = "Produtos naturais",
        ["nutrition_supplements"] = "Suplementos",
        ["greengrocer"] = "Hortifruti",
        ["farm"] = "Produtos da fazenda",
        ["supermarket"] = "Supermercado",
        ["convenience"] = "Mercearia/Conveniência",
        ["deli"] = "Empório",
        ["spices"] = "Temperos e especiarias",
        ["coffee"] = "Café",
        ["tea"] = "Chás",
        ["bakery"] = "Padaria",
        ["confectionery"] = "Doces e confeitaria",
        ["chemist"] = "Farmácia/Drogaria",
    };

    public record ConcorrenteOsm(string OsmRef, string Nome, string? Categoria,
        double Lat, double Lng, string? Endereco, string? Telefone, string? Website, decimal DistanciaKm);

    public record Ponto(double Lat, double Lng);

    private static void PrepararHeaders(HttpRequestMessage req)
    {
        // Nominatim/Overpass exigem User-Agent identificável.
        req.Headers.UserAgent.ParseAdd("EcoGranel-Sistema/1.0 (contato: alexandro@wtcbrasil.com)");
        req.Headers.AcceptLanguage.ParseAdd("pt-BR");
    }

    /// <summary>
    /// Geocodifica pelo endereço estruturado (Brasil), mais confiável que texto livre.
    /// Tenta: (1) rua+número+cidade+UF+CEP; (2) sem CEP; (3) só CEP+cidade. Retorna null
    /// se nada resolver. Ignora complemento (ex.: "Loja 18"), que atrapalha o Nominatim.
    /// </summary>
    public async Task<Ponto?> GeocodificarAsync(string? logradouro, string? numero, string? bairro,
        string? cidade, string? uf, string? cep, CancellationToken ct = default)
    {
        var cepLimpo = new string((cep ?? "").Where(char.IsDigit).ToArray());
        var street = string.Join(" ", new[] { numero, logradouro }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

        // Tentativa 1: estruturado completo.
        var p = await ConsultarEstruturadoAsync(street, bairro, cidade, uf, cepLimpo, ct);
        if (p is not null) return p;

        // Tentativa 2: estruturado sem CEP (CEP às vezes não bate no dataset do OSM).
        await Task.Delay(1100, ct);
        p = await ConsultarEstruturadoAsync(street, bairro, cidade, uf, null, ct);
        if (p is not null) return p;

        // Tentativa 3: só CEP + cidade (centroide do CEP).
        if (cepLimpo.Length == 8)
        {
            await Task.Delay(1100, ct);
            p = await ConsultarEstruturadoAsync(null, null, cidade, uf, cepLimpo, ct);
        }
        return p;
    }

    private async Task<Ponto?> ConsultarEstruturadoAsync(string? street, string? bairro,
        string? cidade, string? uf, string? cep, CancellationToken ct)
    {
        var q = new List<string> { "format=jsonv2", "limit=1", "countrycodes=br" };
        // Nominatim aceita "city" com bairro embutido; mandamos bairro junto da cidade.
        var cidadeQ = string.Join(" ", new[] { bairro, cidade }.Where(s => !string.IsNullOrWhiteSpace(s)));
        void Add(string k, string? v) { if (!string.IsNullOrWhiteSpace(v)) q.Add($"{k}={Uri.EscapeDataString(v)}"); }
        Add("street", street);
        Add("city", cidadeQ);
        Add("state", uf);
        Add("postalcode", cep);
        if (q.Count <= 3) return null; // nada além dos flags

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{NominatimUrl}?{string.Join("&", q)}");
        PrepararHeaders(req);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
        var el = doc.RootElement[0];
        if (!el.TryGetProperty("lat", out var latEl) || !el.TryGetProperty("lon", out var lonEl)) return null;
        if (double.TryParse(latEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(lonEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
            return new Ponto(lat, lng);
        return null;
    }

    /// <summary>Busca concorrentes (lojas de alimentação/mercado) num raio (metros) do ponto.</summary>
    public async Task<List<ConcorrenteOsm>> BuscarConcorrentesAsync(double lat, double lng,
        int raioMetros, CancellationToken ct = default)
    {
        var shops = string.Join("|", ShopLabels.Keys);
        var ql =
            "[out:json][timeout:25];(" +
            $"node[\"shop\"~\"^({shops})$\"](around:{raioMetros},{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)});" +
            $"way[\"shop\"~\"^({shops})$\"](around:{raioMetros},{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)});" +
            ");out center tags;";

        using var req = new HttpRequestMessage(HttpMethod.Post, OverpassUrl)
        {
            Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", ql) })
        };
        PrepararHeaders(req);
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(body);
        var resultado = new List<ConcorrenteOsm>();
        if (!doc.RootElement.TryGetProperty("elements", out var elems)) return resultado;

        foreach (var e in elems.EnumerateArray())
        {
            var tipo = e.GetProperty("type").GetString() ?? "node";
            if (!e.TryGetProperty("id", out var idEl)) continue;
            var osmRef = $"{tipo}/{idEl.GetInt64()}";

            double elat, elng;
            if (tipo == "node")
            {
                if (!e.TryGetProperty("lat", out var la) || !e.TryGetProperty("lon", out var lo)) continue;
                elat = la.GetDouble(); elng = lo.GetDouble();
            }
            else
            {
                if (!e.TryGetProperty("center", out var c)) continue;
                elat = c.GetProperty("lat").GetDouble(); elng = c.GetProperty("lon").GetDouble();
            }

            if (!e.TryGetProperty("tags", out var tags)) continue;
            var nome = Tag(tags, "name");
            if (string.IsNullOrWhiteSpace(nome)) continue;   // sem nome não serve

            var shop = Tag(tags, "shop");
            var categoria = shop is not null && ShopLabels.TryGetValue(shop, out var lbl) ? lbl : shop;

            var endereco = MontarEndereco(tags);
            var telefone = Tag(tags, "phone") ?? Tag(tags, "contact:phone");
            var website = Tag(tags, "website") ?? Tag(tags, "contact:website");
            var dist = (decimal)Math.Round(HaversineKm(lat, lng, elat, elng), 2);

            resultado.Add(new ConcorrenteOsm(osmRef, nome!, categoria, elat, elng, endereco, telefone, website, dist));
        }

        // dedupe por osmRef e ordena por distância
        return resultado
            .GroupBy(r => r.OsmRef).Select(g => g.First())
            .OrderBy(r => r.DistanciaKm)
            .ToList();
    }

    private static string? Tag(JsonElement tags, string key)
        => tags.TryGetProperty(key, out var v) ? v.GetString() : null;

    private static string? MontarEndereco(JsonElement tags)
    {
        var rua = Tag(tags, "addr:street");
        var num = Tag(tags, "addr:housenumber");
        var bairro = Tag(tags, "addr:suburb") ?? Tag(tags, "addr:neighbourhood");
        var partes = new[]
        {
            string.Join(", ", new[] { rua, num }.Where(s => !string.IsNullOrWhiteSpace(s))),
            bairro
        }.Where(s => !string.IsNullOrWhiteSpace(s));
        var full = string.Join(" - ", partes);
        return string.IsNullOrWhiteSpace(full) ? null : full;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = Deg(lat2 - lat1), dLon = Deg(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(Deg(lat1)) * Math.Cos(Deg(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
    private static double Deg(double d) => d * Math.PI / 180.0;
}
