using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Desempenho.Entities;

/// <summary>Aceite digital do Termo de Regulamento de Premiação por Desempenho,
/// com evidências (face, assinatura, geolocalização, IP, data/hora) para valor probatório.</summary>
public class AceiteTermoPremiacao : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ColaboradorId { get; private set; }
    public string ColaboradorNome { get; private set; } = null!;
    public string TermoVersao { get; private set; } = null!;
    public string TermoHash { get; private set; } = null!;      // SHA-256 do texto aceito
    public DateTime DataAceite { get; private set; }

    // Evidências (data URLs base64)
    public string? FotoBase64 { get; private set; }             // face capturada na webcam
    public string? AssinaturaBase64 { get; private set; }       // assinatura desenhada

    // Geolocalização
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public double? PrecisaoMetros { get; private set; }

    // Trilha de auditoria
    public string? Ip { get; private set; }
    public string? UserAgent { get; private set; }

    private AceiteTermoPremiacao() { }

    public static AceiteTermoPremiacao Criar(Guid empresaId, Guid colaboradorId, string colaboradorNome,
        string termoVersao, string termoHash, string? fotoBase64, string? assinaturaBase64,
        double? latitude, double? longitude, double? precisaoMetros, string? ip, string? userAgent)
        => new()
        {
            EmpresaId = empresaId, ColaboradorId = colaboradorId, ColaboradorNome = colaboradorNome,
            TermoVersao = termoVersao, TermoHash = termoHash, DataAceite = DateTime.UtcNow,
            FotoBase64 = fotoBase64, AssinaturaBase64 = assinaturaBase64,
            Latitude = latitude, Longitude = longitude, PrecisaoMetros = precisaoMetros,
            Ip = ip, UserAgent = userAgent,
        };
}
