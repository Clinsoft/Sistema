using Sistema.Domain.Fiscal.Entities;

namespace Sistema.Domain.Fiscal.Interfaces;

/// <summary>
/// Serviço de Distribuição DFe (nfeDistDFeInt) — consulta NF-e recebidas na SEFAZ.
/// Implementação real usa DFe.NET com certificado A1/A3.
/// </summary>
public interface IDistribuicaoDFeService
{
    /// <summary>
    /// Consulta NF-e emitidas contra o CNPJ da empresa a partir do último NSU consultado.
    /// Retorna lista de documentos encontrados na SEFAZ.
    /// </summary>
    Task<ResultadoConsultaDFe> ConsultarAsync(
        string cnpj, string uf, string ultimoNSU, CancellationToken ct);

    /// <summary>
    /// Realiza manifestação do destinatário para uma NF-e.
    /// </summary>
    Task<bool> ManifestarAsync(
        string cnpj, string uf, string chaveAcesso,
        ManifestacaoTipo tipo, string? justificativa, CancellationToken ct);

    /// <summary>
    /// Baixa o XML completo de uma NF-e autorizada.
    /// </summary>
    Task<string?> BaixarXmlAsync(
        string cnpj, string uf, string chaveAcesso, CancellationToken ct);
}

public record ResultadoConsultaDFe(
    bool Sucesso,
    string? Erro,
    string UltimoNSU,
    IReadOnlyList<DFeDocumento> Documentos,
    string MaxNSU = "0")
{
    /// <summary>
    /// true enquanto o último NSU processado for menor que o maior NSU disponível
    /// (ultNSU &lt; maxNSU) — protocolo oficial de paginação da Distribuição DFe.
    /// Não depende de os documentos do lote terem sido parseados com sucesso.
    /// </summary>
    public bool TemMais =>
        long.TryParse(UltimoNSU, out var u) &&
        long.TryParse(MaxNSU, out var m) && m > u;
};

public record DFeDocumento(
    string ChaveAcesso,
    string NSU,
    string Modelo,
    string Serie,
    long Numero,
    DateTime DataEmissao,
    string EmitenteCnpj,
    string EmitenteNome,
    string? EmitenteUF,
    decimal ValorTotal,
    SituacaoNFeRecebida Situacao);
