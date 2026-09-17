using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Estabelecimento concorrente mapeado num raio em volta de uma loja (LocalEstoque).
/// A Fase 1 popula via OpenStreetMap (Overpass); os preços vêm nas fases seguintes.
/// </summary>
public class Concorrente : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }   // loja cujo raio contém este concorrente

    public string Nome { get; private set; } = null!;
    public string? Categoria { get; private set; }     // rótulo amigável (ex.: "Supermercado")
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public decimal DistanciaKm { get; private set; }

    public string? Endereco { get; private set; }
    public string? Telefone { get; private set; }
    public string? Website { get; private set; }

    public string Fonte { get; private set; } = "OSM";  // OSM | Manual
    public string? OsmRef { get; private set; }          // ex.: "node/123" — dedupe por loja

    // Fase 4 (monitoramento online) — já previsto para não gerar nova migration depois.
    public bool MonitorarOnline { get; private set; }
    public string? UrlOnline { get; private set; }

    public bool Ativo { get; private set; } = true;

    private Concorrente() { }

    public static Concorrente CriarDoOsm(Guid empresaId, Guid localEstoqueId, string nome,
        string? categoria, double latitude, double longitude, decimal distanciaKm,
        string? endereco, string? telefone, string? website, string osmRef)
        => CriarExterno(empresaId, localEstoqueId, "OSM", nome, categoria, latitude, longitude,
            distanciaKm, endereco, telefone, website, osmRef);

    /// <summary>Cria a partir de uma fonte externa (OSM ou Google). `refExterna` = id p/ dedupe.</summary>
    public static Concorrente CriarExterno(Guid empresaId, Guid localEstoqueId, string fonte, string nome,
        string? categoria, double latitude, double longitude, decimal distanciaKm,
        string? endereco, string? telefone, string? website, string refExterna)
        => new()
        {
            EmpresaId = empresaId, LocalEstoqueId = localEstoqueId, Nome = nome,
            Categoria = categoria, Latitude = latitude, Longitude = longitude,
            DistanciaKm = distanciaKm, Endereco = endereco, Telefone = telefone,
            Website = website, Fonte = fonte, OsmRef = refExterna,
        };

    public static Concorrente CriarManual(Guid empresaId, Guid localEstoqueId, string nome,
        string? categoria, double latitude, double longitude, decimal distanciaKm, string? endereco)
        => new()
        {
            EmpresaId = empresaId, LocalEstoqueId = localEstoqueId, Nome = nome,
            Categoria = categoria, Latitude = latitude, Longitude = longitude,
            DistanciaKm = distanciaKm, Endereco = endereco, Fonte = "Manual",
        };

    /// <summary>Atualiza os dados vindos de uma nova varredura do OSM (mantém o registro).</summary>
    public void AtualizarDoOsm(string nome, string? categoria, double latitude, double longitude,
        decimal distanciaKm, string? endereco, string? telefone, string? website)
    {
        Nome = nome; Categoria = categoria; Latitude = latitude; Longitude = longitude;
        DistanciaKm = distanciaKm; Endereco = endereco; Telefone = telefone; Website = website;
    }

    public void DefinirMonitoramento(bool monitorar, string? url) { MonitorarOnline = monitorar; UrlOnline = url; }
    public void Ativar() => Ativo = true;
    public void Desativar() => Ativo = false;
}
