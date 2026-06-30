using Microsoft.Extensions.Logging;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Transmite NF-e/NFC-e assinada para o webservice de autorização da SEFAZ
/// usando SOAP 1.2 com TLS mútuo (certificado A1).
/// </summary>
public class NFeTransmissaoService(ILogger<NFeTransmissaoService> logger) : INFeTransmissaoService
{
    // ── URLs ──────────────────────────────────────────────────────────────────
    // NF-e (mod 55) SP
    private const string UrlNFeProducao    = "https://nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx";
    private const string UrlNFeHomologacao = "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx";
    // NFC-e (mod 65) SP
    private const string UrlNFCeProducao    = "https://nfce.fazenda.sp.gov.br/ws/nfceautorizacao4.asmx";
    private const string UrlNFCeHomologacao = "https://homologacao.nfce.fazenda.sp.gov.br/ws/nfceautorizacao4.asmx";

    // ── SOAP actions ──────────────────────────────────────────────────────────
    private const string ActionNFe  = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4/nfeAutorizacaoLote";
    private const string ActionNFCe = "http://www.portalfiscal.inf.br/nfe/wsdl/NFCeAutorizacao4/nfceAutorizacaoLote";

    public async Task<ResultadoTransmissao> TransmitirAsync(
        string xmlAssinado,
        ConfiguracaoFiscal config,
        CancellationToken ct = default)
    {
        // Detectar modelo a partir do conteúdo XML (mod 55 ou 65)
        var isNFCe = xmlAssinado.Contains("<mod>65</mod>");
        var url = (isNFCe, config.Ambiente) switch
        {
            (true,  AmbienteFiscal.Producao)    => UrlNFCeProducao,
            (true,  _)                           => UrlNFCeHomologacao,
            (false, AmbienteFiscal.Producao)    => UrlNFeProducao,
            (false, _)                           => UrlNFeHomologacao
        };
        var action = isNFCe ? ActionNFCe : ActionNFe;
        var wsdlNs = isNFCe
            ? "http://www.portalfiscal.inf.br/nfe/wsdl/NFCeAutorizacao4"
            : "http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4";
        var tpAmb = config.Ambiente == AmbienteFiscal.Producao ? "1" : "2";

        try
        {
            var cert = CarregarCertificado(config);
            if (cert is null)
                return new ResultadoTransmissao(false, null, "Certificado digital não configurado.", null);

            var idLote = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var envelope = MontarEnvelopeSOAP(xmlAssinado, wsdlNs, idLote, tpAmb);

            logger.LogInformation("Transmitindo NF-e para SEFAZ: {Url}", url);
            var responseXml = await EnviarSOAPAsync(url, action, envelope, cert, ct);
            logger.LogInformation("Resposta SEFAZ: {Response}",
                responseXml[..Math.Min(600, responseXml.Length)]);

            return ParsearResposta(responseXml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao transmitir NF-e para SEFAZ");
            return new ResultadoTransmissao(false, null, $"Erro de comunicação: {ex.Message}", null);
        }
    }

    // ── Montagem do envelope SOAP 1.2 ─────────────────────────────────────────

    private static string MontarEnvelopeSOAP(string xmlNFe, string wsdlNs, string idLote, string tpAmb)
    {
        // Extrair o cUF do XML (usado no cabeçalho)
        var cUF = "35"; // SP padrão; extrai do XML se presente
        var mCUF = System.Text.RegularExpressions.Regex.Match(xmlNFe, @"<cUF>(\d+)</cUF>");
        if (mCUF.Success) cUF = mCUF.Groups[1].Value;

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <soap12:Envelope xmlns:soap12="http://www.w3.org/2003/05/soap-envelope">
              <soap12:Header>
                <nfeCabecMsg xmlns="{wsdlNs}">
                  <cUF>{cUF}</cUF>
                  <versaoDados>4.00</versaoDados>
                </nfeCabecMsg>
              </soap12:Header>
              <soap12:Body>
                <nfeDadosMsg xmlns="{wsdlNs}">
                  <enviNFe versao="4.00" xmlns="http://www.portalfiscal.inf.br/nfe">
                    <idLote>{idLote}</idLote>
                    <indSinc>1</indSinc>
                    {xmlNFe}
                  </enviNFe>
                </nfeDadosMsg>
              </soap12:Body>
            </soap12:Envelope>
            """;
    }

    // ── Envio HTTP ────────────────────────────────────────────────────────────

    private static async Task<string> EnviarSOAPAsync(
        string url, string action, string envelope,
        X509Certificate2 cert, CancellationToken ct)
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { cert },
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
                EnabledSslProtocols =
                    System.Security.Authentication.SslProtocols.Tls12 |
                    System.Security.Authentication.SslProtocols.Tls13
            }
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var bytes   = Encoding.UTF8.GetBytes(envelope);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType =
            MediaTypeHeaderValue.Parse($"application/soap+xml;charset=utf-8;action=\"{action}\"");

        var response = await client.PostAsync(url, content, ct);
        var body     = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"SEFAZ HTTP {(int)response.StatusCode}: {body[..Math.Min(800, body.Length)]}");

        return body;
    }

    // ── Parse da resposta ────────────────────────────────────────────────────

    private static ResultadoTransmissao ParsearResposta(string xml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("nfe",    "http://www.portalfiscal.inf.br/nfe");
            ns.AddNamespace("soap12", "http://www.w3.org/2003/05/soap-envelope");

            // Verifica SOAP Fault
            var fault = doc.SelectSingleNode("//*[local-name()='Fault']");
            if (fault != null)
            {
                var msg = fault.SelectSingleNode("*[local-name()='Text']")?.InnerText
                       ?? fault.SelectSingleNode("faultstring")?.InnerText
                       ?? "Erro SOAP SEFAZ";
                return new ResultadoTransmissao(false, null, msg, xml);
            }

            // Resposta do lote: retEnviNFe → infRec ou protNFe
            var cStat  = doc.SelectSingleNode("//nfe:cStat",  ns)?.InnerText ?? "";
            var xMot   = doc.SelectSingleNode("//nfe:xMotivo",ns)?.InnerText ?? "";
            var nProt  = doc.SelectSingleNode("//nfe:nProt",  ns)?.InnerText;

            // cStat 100=autorizada, 150=autorizada fora do prazo, 204=duplicata (já autorizada)
            var autorizada = cStat is "100" or "150" or "204";

            return autorizada
                ? new ResultadoTransmissao(true, nProt, null, xml)
                : new ResultadoTransmissao(false, null, $"SEFAZ {cStat}: {xMot}", xml);
        }
        catch (Exception ex)
        {
            return new ResultadoTransmissao(false, null, $"Erro ao parsear resposta: {ex.Message}", xml);
        }
    }

    // ── Carregar certificado A1 ───────────────────────────────────────────────

    private static X509Certificate2? CarregarCertificado(ConfiguracaoFiscal config)
    {
        if (config.CertificadoPfxBase64 is null) return null;

        var bytes = Convert.FromBase64String(config.CertificadoPfxBase64);
        var flags = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.Linux)
            ? X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet
            : X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet;

        return new X509Certificate2(bytes, config.CertificadoSenha, flags);
    }
}
