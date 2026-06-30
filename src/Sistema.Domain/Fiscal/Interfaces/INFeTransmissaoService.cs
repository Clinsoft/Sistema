using Sistema.Domain.Fiscal.Entities;

namespace Sistema.Domain.Fiscal.Interfaces;

/// <summary>Serviço de transmissão de NF-e/NFC-e para a SEFAZ (autorização lote síncrono).</summary>
public interface INFeTransmissaoService
{
    /// <summary>
    /// Transmite o XML assinado para o webservice de autorização da SEFAZ.
    /// </summary>
    Task<ResultadoTransmissao> TransmitirAsync(
        string xmlAssinado,
        ConfiguracaoFiscal config,
        CancellationToken ct = default);
}

/// <param name="Autorizada">true quando cStat == 100 ou 150.</param>
/// <param name="Protocolo">nProt retornado pela SEFAZ (preenchido quando autorizada).</param>
/// <param name="MotivoRejeicao">xMotivo da SEFAZ (preenchido quando não autorizada).</param>
/// <param name="XmlRetorno">Envelope SOAP de resposta completo.</param>
public record ResultadoTransmissao(
    bool Autorizada,
    string? Protocolo,
    string? MotivoRejeicao,
    string? XmlRetorno);
