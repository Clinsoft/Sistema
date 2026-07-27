using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Infrastructure.Data;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Integração com o webservice nfeDistDFeInt da SEFAZ Nacional.
/// Usa SOAP 1.2 (HTTP POST), TLS mútuo e assinatura XML (RSA-SHA1).
/// </summary>
public class DistribuicaoDFeService(
    SistemaDbContext db,
    ILogger<DistribuicaoDFeService> logger) : IDistribuicaoDFeService
{
    private const string UrlProducao    = "https://www1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx";
    private const string UrlHomologacao = "https://hom1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx";
    private const string SoapAction     = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe/nfeDistDFeInteresse";

    // Distribuição DFe de CT-e (webservice próprio, separado do de NF-e).
    private const string CteUrlProducao    = "https://www1.cte.fazenda.gov.br/CTeDistribuicaoDFe/CTeDistribuicaoDFe.asmx";
    private const string CteUrlHomologacao = "https://hom1.cte.fazenda.gov.br/CTeDistribuicaoDFe/CTeDistribuicaoDFe.asmx";
    private const string CteWsdlNs         = "http://www.portalfiscal.inf.br/cte/wsdl/CTeDistribuicaoDFe";
    private const string CteSoapAction     = "http://www.portalfiscal.inf.br/cte/wsdl/CTeDistribuicaoDFe/cteDistDFeInteresse";

    // NFeRecepcaoEvento4 — manifestação do destinatário: processada pelo
    // Ambiente Nacional (AN), não pela SEFAZ estadual. cOrgao do evento = 91.
    private const string EventoUrlProducao    = "https://www1.nfe.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx";
    private const string EventoUrlHomologacao = "https://hom1.nfe.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx";
    private const string EventoSoapAction     = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4/nfeRecepcaoEvento";

    public async Task<ResultadoConsultaDFe> ConsultarAsync(
        string cnpj, string uf, string ultimoNSU, CancellationToken ct)
    {
        try
        {
            var (cert, config) = await CarregarAsync(cnpj, ct);
            if (cert is null || config is null)
                return Falha("Certificado digital A1 não configurado. Acesse Configurações → Fiscal para cadastrar o certificado.", ultimoNSU);

            var url         = config.Ambiente == AmbienteFiscal.Producao ? UrlProducao : UrlHomologacao;
            var ambienteInt = config.Ambiente == AmbienteFiscal.Producao ? 1 : 2;
            var cUFAutor = UfParaCodigo(uf);
            var nsuFmt  = (ultimoNSU == "0" || ultimoNSU == "") ? "000000000000000" : ultimoNSU.PadLeft(15, '0');
            var distXml = $"<distDFeInt versao=\"1.01\" xmlns=\"http://www.portalfiscal.inf.br/nfe\"><tpAmb>{ambienteInt}</tpAmb><cUFAutor>{cUFAutor}</cUFAutor><CNPJ>{cnpj}</CNPJ><distNSU><ultNSU>{nsuFmt}</ultNSU></distNSU></distDFeInt>";

            logger.LogInformation("SEFAZ DFe XML: {Xml}", distXml);
            var responseXml = await EnviarAsync(url, distXml, cert, ct);
            logger.LogInformation("SEFAZ DFe response: {Response}", responseXml[..Math.Min(500, responseXml.Length)]);
            return ParsearResposta(responseXml, ultimoNSU);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao consultar NF-e recebidas na SEFAZ para CNPJ {Cnpj}", cnpj);
            return Falha($"Erro ao consultar SEFAZ: {ex.Message}", ultimoNSU);
        }
    }

    public async Task<bool> ManifestarAsync(
        string cnpj, string uf, string chaveAcesso,
        ManifestacaoTipo tipo, string? justificativa, CancellationToken ct)
    {
        try
        {
            var (cert, config) = await CarregarAsync(cnpj, ct);
            if (cert is null || config is null)
            {
                logger.LogWarning("Manifestação cancelada: certificado não configurado para CNPJ {Cnpj}", cnpj);
                return false;
            }

            var url         = config.Ambiente == AmbienteFiscal.Producao ? EventoUrlProducao : EventoUrlHomologacao;
            var ambienteInt = config.Ambiente == AmbienteFiscal.Producao ? 1 : 2;

            var tpEvento   = (int)tipo;
            // descEvento é uma enumeração fixa do schema — sem acentos e com grafia exata.
            var descEvento = tipo switch
            {
                ManifestacaoTipo.CienciaOperacao        => "Ciencia da Operacao",
                ManifestacaoTipo.ConfirmacaoOperacao     => "Confirmacao da Operacao",
                ManifestacaoTipo.DesconhecimentoOperacao => "Desconhecimento da Operacao",
                ManifestacaoTipo.OperacaoNaoRealizada    => "Operacao nao Realizada",
                _ => throw new ArgumentOutOfRangeException(nameof(tipo))
            };

            var dhEvento = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
            var idEvento = $"ID{tpEvento}{chaveAcesso}01";

            // Operação Não Realizada requer justificativa (xJust, mínimo 15 chars)
            var detEventoInterno = tipo == ManifestacaoTipo.OperacaoNaoRealizada
                ? $"<descEvento>{descEvento}</descEvento><xJust>{System.Security.SecurityElement.Escape(justificativa ?? "Operação não reconhecida pelo destinatário.")}</xJust>"
                : $"<descEvento>{descEvento}</descEvento>";

            var infEventoXml = $"""
                <infEvento Id="{idEvento}">
                  <cOrgao>91</cOrgao>
                  <tpAmb>{ambienteInt}</tpAmb>
                  <CNPJ>{cnpj}</CNPJ>
                  <chNFe>{chaveAcesso}</chNFe>
                  <dhEvento>{dhEvento}</dhEvento>
                  <tpEvento>{tpEvento}</tpEvento>
                  <nSeqEvento>1</nSeqEvento>
                  <verEvento>1.00</verEvento>
                  <detEvento versao="1.00">{detEventoInterno}</detEvento>
                </infEvento>
                """;

            var eventoAssinadoXml = AssinarEvento(infEventoXml, idEvento, cert);

            var envEvento = $"""
                <envEvento versao="1.00" xmlns="http://www.portalfiscal.inf.br/nfe">
                  <idLote>1</idLote>
                  <evento versao="1.00">{eventoAssinadoXml}</evento>
                </envEvento>
                """;

            logger.LogInformation("SEFAZ Manifestação tipo={Tipo} chave={Chave}", tipo, chaveAcesso);
            var responseXml = await EnviarEventoAsync(url, envEvento, cert, ct);
            logger.LogInformation("SEFAZ Manifestação response: {Response}", responseXml[..Math.Min(500, responseXml.Length)]);

            return ParsearRespostaEvento(responseXml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao manifestar NF-e {Chave} tipo {Tipo}", chaveAcesso, tipo);
            return false;
        }
    }

    public async Task<string?> BaixarXmlAsync(
        string cnpj, string uf, string chaveAcesso, CancellationToken ct)
    {
        try
        {
            var (cert, config) = await CarregarAsync(cnpj, ct);
            if (cert is null || config is null) return null;

            var url         = config.Ambiente == AmbienteFiscal.Producao ? UrlProducao : UrlHomologacao;
            var ambienteInt = config.Ambiente == AmbienteFiscal.Producao ? 1 : 2;
            var cUF         = UfParaCodigo(uf);

            var distXml = $"""
                <distDFeInt versao="1.01" xmlns="http://www.portalfiscal.inf.br/nfe">
                  <tpAmb>{ambienteInt}</tpAmb>
                  <cUFAutor>{cUF}</cUFAutor>
                  <CNPJ>{cnpj}</CNPJ>
                  <consChNFe><chNFe>{chaveAcesso}</chNFe></consChNFe>
                </distDFeInt>
                """;

            var responseXml = await EnviarAsync(url, distXml, cert, ct);
            logger.LogInformation("consChNFe {Chave} → resposta SEFAZ: {Xml}", chaveAcesso, responseXml);
            return ExtrairXmlNota(responseXml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao baixar XML da NF-e {Chave}", chaveAcesso);
            return null;
        }
    }

    public async Task<DFeDocumento?> ConsultarPorChaveAsync(
        string cnpj, string uf, string chaveAcesso, CancellationToken ct)
    {
        // Consulta por chave só existe para NF-e (consChNFe). CT-e é recebido pelo
        // pull sequencial (ConsultarCTeAsync).
        var xml = await BaixarXmlAsync(cnpj, uf, chaveAcesso, ct);
        if (string.IsNullOrEmpty(xml)) return null;
        var doc = ParsearDocumento(xml, "0");
        // Se a chave não veio no XML (ex.: procCTe sem Id), usa a chave consultada.
        if (doc != null && string.IsNullOrWhiteSpace(doc.ChaveAcesso))
            doc = doc with { ChaveAcesso = chaveAcesso };
        return doc;
    }

    public async Task<ResultadoConsultaDFe> ConsultarCTeAsync(
        string cnpj, string uf, string ultimoNSU, CancellationToken ct)
    {
        try
        {
            var (cert, config) = await CarregarAsync(cnpj, ct);
            if (cert is null || config is null)
                return Falha("Certificado digital A1 não configurado.", ultimoNSU);

            var url = config.Ambiente == AmbienteFiscal.Producao ? CteUrlProducao : CteUrlHomologacao;
            var amb = config.Ambiente == AmbienteFiscal.Producao ? 1 : 2;
            var cUF = UfParaCodigo(uf);
            var nsuFmt = (ultimoNSU == "0" || ultimoNSU == "") ? "000000000000000" : ultimoNSU.PadLeft(15, '0');

            // Distribuição sequencial de CT-e (distNSU) — sem espaços entre as tags.
            var distXml =
                $"<distDFeInt versao=\"1.00\" xmlns=\"http://www.portalfiscal.inf.br/cte\">" +
                $"<tpAmb>{amb}</tpAmb><cUFAutor>{cUF}</cUFAutor><CNPJ>{cnpj}</CNPJ>" +
                $"<distNSU><ultNSU>{nsuFmt}</ultNSU></distNSU></distDFeInt>";

            var responseXml = await EnviarAsync(url, distXml, cert, ct,
                "cteDistDFeInteresse", "cteDadosMsg", CteWsdlNs, CteSoapAction);
            logger.LogInformation("SEFAZ CT-e DFe response: {Response}", responseXml[..Math.Min(500, responseXml.Length)]);
            return ParsearResposta(responseXml, ultimoNSU);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao consultar CT-e recebidos na SEFAZ para CNPJ {Cnpj}", cnpj);
            return Falha($"Erro ao consultar CT-e na SEFAZ: {ex.Message}", ultimoNSU);
        }
    }

    // ── Assinatura RSA-SHA1 do infEvento ──────────────────────────────────

    private static string AssinarEvento(string infEventoXml, string idEvento, X509Certificate2 cert)
    {
        var doc = new XmlDocument { PreserveWhitespace = false };
        doc.LoadXml($"<evento xmlns=\"http://www.portalfiscal.inf.br/nfe\">{infEventoXml}</evento>");

        if (doc.DocumentElement!.FirstChild is not XmlElement infNode)
            throw new InvalidOperationException("infEvento não encontrado.");

        var signed = new SignedXml(doc)
        {
            SigningKey = cert.GetRSAPrivateKey()
                ?? throw new InvalidOperationException("Certificado sem chave RSA privada.")
        };

        signed.SignedInfo.SignatureMethod        = SignedXml.XmlDsigRSASHA1Url;
        signed.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

        var reference = new Reference($"#{idEvento}");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        reference.DigestMethod = SignedXml.XmlDsigSHA1Url;
        signed.AddReference(reference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signed.KeyInfo = keyInfo;

        signed.ComputeSignature();

        // A <Signature> deve ser IRMÃ de <infEvento> (ambas filhas de <evento>),
        // não filha de infEvento. Anexa ao elemento raiz <evento> e retorna o
        // conteúdo interno (infEvento + Signature) para o wrapper montar o <evento>.
        _ = infNode;
        doc.DocumentElement!.AppendChild(doc.ImportNode(signed.GetXml(), true));
        return doc.DocumentElement.InnerXml;
    }

    // ── Envio SOAP — NFeRecepcaoEvento4 ───────────────────────────────────

    private static async Task<string> EnviarEventoAsync(
        string url, string corpoXml, X509Certificate2 cert, CancellationToken ct)
    {
        var envelope = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <soap12:Envelope xmlns:soap12="http://www.w3.org/2003/05/soap-envelope">
              <soap12:Header>
                <nfeCabecMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4">
                  <cUF>91</cUF><versaoDados>1.00</versaoDados>
                </nfeCabecMsg>
              </soap12:Header>
              <soap12:Body>
                <nfeDadosMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4">
                  {corpoXml}
                </nfeDadosMsg>
              </soap12:Body>
            </soap12:Envelope>
            """;

        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { cert },
                LocalCertificateSelectionCallback = (_, _, _, _, _) => cert,
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13,
            }
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var bytes   = Encoding.UTF8.GetBytes(envelope);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType =
            System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
                $"application/soap+xml;charset=utf-8;action=\"{EventoSoapAction}\"");

        var response = await client.PostAsync(url, content, ct);
        var body     = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"SEFAZ evento {(int)response.StatusCode}: {body[..Math.Min(800, body.Length)]}");

        return body;
    }

    private static bool ParsearRespostaEvento(string xml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("nfe", "http://www.portalfiscal.inf.br/nfe");
            var cStat = doc.SelectSingleNode("//nfe:cStat", ns)?.InnerText ?? "";
            // 135 = Evento registrado e vinculado a NF-e, 136 = Evento vinculado
            return cStat == "135" || cStat == "136";
        }
        catch { return false; }
    }

    // ── Envio SOAP — NFeDistribuicaoDFe ───────────────────────────────────

    private static async Task<string> EnviarAsync(
        string url, string corpoXml, X509Certificate2 cert, CancellationToken ct,
        string operacao = "nfeDistDFeInteresse", string dadosMsg = "nfeDadosMsg",
        string wsdlNs = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe",
        string soapAction = SoapAction)
    {
        // O serviço de distribuição exige o elemento de operação (nfe/cteDistDFeInteresse)
        // envolvendo o *DadosMsg. Sem esse wrapper, o ASMX retorna HTTP 500 genérico.
        var envelope = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <soap12:Envelope xmlns:soap12="http://www.w3.org/2003/05/soap-envelope">
              <soap12:Body>
                <{operacao} xmlns="{wsdlNs}">
                  <{dadosMsg}>
                    {corpoXml}
                  </{dadosMsg}>
                </{operacao}>
              </soap12:Body>
            </soap12:Envelope>
            """;

        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { cert },
                LocalCertificateSelectionCallback = (_, _, _, _, _) => cert,
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13,
            }
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var bytes   = Encoding.UTF8.GetBytes(envelope);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType =
            System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
                $"application/soap+xml;charset=utf-8;action=\"{soapAction}\"");

        var response = await client.PostAsync(url, content, ct);
        var body     = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            if (body.Contains("Object reference not set to an instance of an object"))
                throw new HttpRequestException(
                    "SEFAZ retornou erro genérico (Object reference). Requisição inválida ou " +
                    "CNPJ não habilitado para Distribuição DFe. Verifique o certificado e o credenciamento do CNPJ.");
            throw new HttpRequestException($"SEFAZ {(int)response.StatusCode}: {body[..Math.Min(800, body.Length)]}");
        }

        return body;
    }

    // ── Parsear resposta ───────────────────────────────────────────────────

    private static ResultadoConsultaDFe ParsearResposta(string xml, string ultimoNSU)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("nfe", "http://www.portalfiscal.inf.br/nfe");
        ns.AddNamespace("soap12", "http://www.w3.org/2003/05/soap-envelope");
        ns.AddNamespace("soap11", "http://schemas.xmlsoap.org/soap/envelope/");

        var fault = doc.SelectSingleNode("//soap12:Fault", ns)
                 ?? doc.SelectSingleNode("//soap11:Fault", ns)
                 ?? doc.SelectSingleNode("//*[local-name()='Fault']");
        if (fault != null)
        {
            var msg = fault.SelectSingleNode("*[local-name()='Text']")?.InnerText
                   ?? fault.SelectSingleNode("faultstring")?.InnerText
                   ?? "Erro SEFAZ";
            return Falha(msg, ultimoNSU);
        }

        // local-name() → funciona tanto para a resposta de NF-e quanto de CT-e.
        var cStat  = doc.SelectSingleNode("//*[local-name()='cStat']")?.InnerText ?? "";
        var xMot   = doc.SelectSingleNode("//*[local-name()='xMotivo']")?.InnerText ?? "";
        var ultNSU = doc.SelectSingleNode("//*[local-name()='ultNSU']")?.InnerText ?? ultimoNSU;
        var maxNSU = doc.SelectSingleNode("//*[local-name()='maxNSU']")?.InnerText ?? ultNSU;

        if (cStat != "137" && cStat != "138")
            return Falha($"SEFAZ {cStat}: {xMot}", ultimoNSU);

        var docs  = new List<DFeDocumento>();
        var nodes = doc.SelectNodes("//*[local-name()='docZip']");
        if (nodes != null)
        {
            foreach (XmlNode node in nodes)
            {
                try
                {
                    var nsu   = node.Attributes?["NSU"]?.Value ?? "0";
                    var bytes = Convert.FromBase64String(node.InnerText);
                    var xmlNota = Encoding.UTF8.GetString(GzipDecompress(bytes));
                    var d = ParsearDocumento(xmlNota, nsu);
                    if (d != null) docs.Add(d);
                }
                catch { }
            }
        }

        static string TrimNsu(string s) => s.TrimStart('0') is "" ? "0" : s.TrimStart('0');
        return new ResultadoConsultaDFe(true, null, TrimNsu(ultNSU), docs, TrimNsu(maxNSU));
    }

    private static DFeDocumento? ParsearDocumento(string xmlNota, string nsu)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlNota);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("n", "http://www.portalfiscal.inf.br/nfe");
            ns.AddNamespace("c", "http://www.portalfiscal.inf.br/cte");

            // Resumo da NF-e (resNFe) — retornado ANTES da manifestação do destinatário.
            var res = doc.SelectSingleNode("//n:resNFe", ns);
            if (res != null) return ParsearResumo(res, ns, nsu);

            // Resumo do CT-e (resCTe) — frete emitido pela transportadora.
            var resCte = doc.SelectSingleNode("//c:resCTe", ns);
            if (resCte != null) return ParsearResumoCTe(resCte, ns, nsu);

            // CT-e completo (procCTe / cteProc).
            var ideCte = doc.SelectSingleNode("//c:CTe//c:ide", ns);
            if (ideCte != null) return ParsearCTeCompleto(doc, ideCte, ns, nsu);

            // NF-e completa (procNFe / nfeProc) — disponível após manifestar.
            var ide = doc.SelectSingleNode("//n:ide", ns);
            if (ide != null) return ParsearNFeCompleta(doc, ide, ns, nsu);

            // Eventos (procEventoNFe / resEvento) e outros documentos → ignora.
            return null;
        }
        catch { return null; }
    }

    /// <summary>Parseia um resumo resNFe. Modelo/série/número são derivados da chave de acesso.</summary>
    private static DFeDocumento? ParsearResumo(XmlNode res, XmlNamespaceManager ns, string nsu)
    {
        var chave = res.SelectSingleNode("n:chNFe", ns)?.InnerText ?? "";
        if (chave.Length != 44) return null;

        var emitCnpj = res.SelectSingleNode("n:CNPJ", ns)?.InnerText
                    ?? res.SelectSingleNode("n:CPF", ns)?.InnerText ?? "";
        var emitNome = res.SelectSingleNode("n:xNome", ns)?.InnerText ?? "";
        var dtStr    = res.SelectSingleNode("n:dhEmi", ns)?.InnerText;
        var dtEmissao = DateTime.TryParse(dtStr, out var dt) ? dt : DateTime.UtcNow;
        var valor = decimal.TryParse(res.SelectSingleNode("n:vNF", ns)?.InnerText,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;

        // Chave: cUF(2) AAMM(4) CNPJ(14) mod(2) serie(3) nNF(9) tpEmis(1) cNF(8) cDV(1)
        var modelo = chave.Substring(20, 2);
        var serieStr = chave.Substring(22, 3).TrimStart('0');
        var serie  = serieStr == "" ? "0" : serieStr;
        var numero = long.TryParse(chave.Substring(25, 9), out var nn) ? nn : 0;
        var emitUF = CodigoParaUf(chave.Substring(0, 2));

        // cSitNFe: 1=autorizada, 3=cancelada
        var situacao = res.SelectSingleNode("n:cSitNFe", ns)?.InnerText == "3"
            ? SituacaoNFeRecebida.Cancelada : SituacaoNFeRecebida.Autorizada;

        return new DFeDocumento(chave, nsu, modelo, serie, numero, dtEmissao,
            emitCnpj, emitNome, emitUF, valor, situacao);
    }

    /// <summary>Parseia um resumo de CT-e (resCTe). Modelo=57; valor = vTPrest (valor do frete).</summary>
    private static DFeDocumento? ParsearResumoCTe(XmlNode res, XmlNamespaceManager ns, string nsu)
    {
        var chave = res.SelectSingleNode("c:chCTe", ns)?.InnerText ?? "";
        if (chave.Length != 44) return null;

        // Emitente = transportadora que emitiu o CT-e.
        var emitCnpj = res.SelectSingleNode("c:CNPJ", ns)?.InnerText
                    ?? res.SelectSingleNode("c:CPF", ns)?.InnerText ?? "";
        var emitNome = res.SelectSingleNode("c:xNome", ns)?.InnerText ?? "";
        var dtStr    = res.SelectSingleNode("c:dhEmi", ns)?.InnerText;
        var dtEmissao = DateTime.TryParse(dtStr, out var dt) ? dt : DateTime.UtcNow;
        var valor = decimal.TryParse(res.SelectSingleNode("c:vTPrest", ns)?.InnerText,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;

        // Chave: cUF(2) AAMM(4) CNPJ(14) mod(2)=57 serie(3) nCT(9) tpEmis(1) cCT(8) cDV(1)
        var modelo = chave.Substring(20, 2);
        var serieStr = chave.Substring(22, 3).TrimStart('0');
        var serie = serieStr == "" ? "0" : serieStr;
        var numero = long.TryParse(chave.Substring(25, 9), out var nn) ? nn : 0;
        var emitUF = CodigoParaUf(chave.Substring(0, 2));

        // cSitCTe: 1=autorizado, 3=cancelado
        var situacao = res.SelectSingleNode("c:cSitCTe", ns)?.InnerText == "3"
            ? SituacaoNFeRecebida.Cancelada : SituacaoNFeRecebida.Autorizada;

        return new DFeDocumento(chave, nsu, modelo, serie, numero, dtEmissao,
            emitCnpj, emitNome, emitUF, valor, situacao);
    }

    /// <summary>Parseia um CT-e completo (procCTe / cteProc). Valor = vTPrest.</summary>
    private static DFeDocumento? ParsearCTeCompleto(XmlDocument doc, XmlNode ide, XmlNamespaceManager ns, string nsu)
    {
        var emit    = doc.SelectSingleNode("//c:emit", ns);
        var infCte  = doc.SelectSingleNode("//c:infCte", ns);

        var chave     = infCte?.Attributes?["Id"]?.Value?.Replace("CTe", "") ?? "";
        if (chave.Length != 44) return null;
        var modelo    = ide.SelectSingleNode("c:mod", ns)?.InnerText ?? "57";
        var serie     = ide.SelectSingleNode("c:serie", ns)?.InnerText ?? "1";
        var numero    = long.TryParse(ide.SelectSingleNode("c:nCT", ns)?.InnerText, out var n) ? n : 0;
        var dtStr     = ide.SelectSingleNode("c:dhEmi", ns)?.InnerText;
        var dtEmissao = DateTime.TryParse(dtStr, out var dt) ? dt : DateTime.UtcNow;
        var emitCnpj  = emit?.SelectSingleNode("c:CNPJ", ns)?.InnerText ?? "";
        var emitNome  = emit?.SelectSingleNode("c:xNome", ns)?.InnerText ?? "";
        var emitUF    = emit?.SelectSingleNode("c:enderEmit/c:UF", ns)?.InnerText;
        var valor     = decimal.TryParse(
            doc.SelectSingleNode("//c:vPrest/c:vTPrest", ns)?.InnerText,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;

        // Chaves das NF-e transportadas por este CT-e (grupo infNFe do infDoc).
        var chavesNFe = new List<string>();
        var nfeNodes = doc.SelectNodes("//c:infNFe/c:chave", ns);
        if (nfeNodes != null)
            foreach (XmlNode ch in nfeNodes)
                if (ch.InnerText is { Length: 44 } k) chavesNFe.Add(k);

        return new DFeDocumento(chave, nsu, modelo, serie, numero, dtEmissao,
            emitCnpj, emitNome, emitUF, valor, SituacaoNFeRecebida.Autorizada,
            chavesNFe.Count > 0 ? chavesNFe : null);
    }

    /// <summary>Parseia a NF-e completa (procNFe / nfeProc).</summary>
    private static DFeDocumento? ParsearNFeCompleta(XmlDocument doc, XmlNode ide, XmlNamespaceManager ns, string nsu)
    {
        var emit   = doc.SelectSingleNode("//n:emit", ns);
        var tot    = doc.SelectSingleNode("//n:ICMSTot", ns);
        var infNFe = doc.SelectSingleNode("//n:infNFe", ns);

        var chave     = infNFe?.Attributes?["Id"]?.Value?.Replace("NFe", "") ?? "";
        var modelo    = ide.SelectSingleNode("n:mod", ns)?.InnerText ?? "55";
        var serie     = ide.SelectSingleNode("n:serie", ns)?.InnerText ?? "1";
        var numero    = long.TryParse(ide.SelectSingleNode("n:nNF", ns)?.InnerText, out var n) ? n : 0;
        var dtStr     = ide.SelectSingleNode("n:dhEmi", ns)?.InnerText ?? ide.SelectSingleNode("n:dEmi", ns)?.InnerText;
        var dtEmissao = DateTime.TryParse(dtStr, out var dt) ? dt : DateTime.UtcNow;
        var emitCnpj  = emit?.SelectSingleNode("n:CNPJ", ns)?.InnerText ?? "";
        var emitNome  = emit?.SelectSingleNode("n:xNome", ns)?.InnerText ?? "";
        var emitUF    = emit?.SelectSingleNode("n:enderEmit/n:UF", ns)?.InnerText;
        var valor     = decimal.TryParse(
            tot?.SelectSingleNode("n:vNF", ns)?.InnerText,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;

        return new DFeDocumento(chave, nsu, modelo, serie, numero, dtEmissao,
            emitCnpj, emitNome, emitUF, valor, SituacaoNFeRecebida.Autorizada);
    }

    private static string? ExtrairXmlNota(string responseXml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(responseXml);
            // docZip pode vir no namespace de NF-e ou de CT-e — usa local-name().
            var node = doc.SelectSingleNode("//*[local-name()='docZip']");
            if (node?.InnerText is { Length: > 0 } b64)
            {
                var bytes = Convert.FromBase64String(b64);
                return Encoding.UTF8.GetString(GzipDecompress(bytes));
            }
        }
        catch { }
        return null;
    }

    // ── Utils ──────────────────────────────────────────────────────────────

    private async Task<(X509Certificate2? cert, ConfiguracaoFiscal? config)> CarregarAsync(
        string cnpj, CancellationToken ct)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.Cnpj == cnpj ||
                e.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "") == cnpj, ct);

        if (empresa is null) return (null, null);

        var config = await db.ConfiguracoesFiscais.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresa.Id, ct);

        if (config?.CertificadoPfxBase64 is null) return (null, config);

        var bytes = Convert.FromBase64String(config.CertificadoPfxBase64);
        var flags = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.Linux)
            ? X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet
            : X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet;
        var cert  = new X509Certificate2(bytes, config.CertificadoSenha, flags);

        return (cert, config);
    }

    private static byte[] GzipDecompress(byte[] data)
    {
        using var ms   = new System.IO.MemoryStream(data);
        using var gz   = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
        using var out_ = new System.IO.MemoryStream();
        gz.CopyTo(out_);
        return out_.ToArray();
    }

    private static ResultadoConsultaDFe Falha(string erro, string ultimoNSU) =>
        new(false, erro, ultimoNSU, [], ultimoNSU);

    private static int UfParaCodigo(string uf) => uf.ToUpper() switch
    {
        "AC" => 12, "AL" => 27, "AP" => 16, "AM" => 13, "BA" => 29,
        "CE" => 23, "DF" => 53, "ES" => 32, "GO" => 52, "MA" => 21,
        "MT" => 51, "MS" => 50, "MG" => 31, "PA" => 15, "PB" => 25,
        "PR" => 41, "PE" => 26, "PI" => 22, "RJ" => 33, "RN" => 24,
        "RS" => 43, "RO" => 11, "RR" => 14, "SC" => 42, "SP" => 35,
        "SE" => 28, "TO" => 17, _ => 35
    };

    private static string? CodigoParaUf(string codigo) => codigo switch
    {
        "12" => "AC", "27" => "AL", "16" => "AP", "13" => "AM", "29" => "BA",
        "23" => "CE", "53" => "DF", "32" => "ES", "52" => "GO", "21" => "MA",
        "51" => "MT", "50" => "MS", "31" => "MG", "15" => "PA", "25" => "PB",
        "41" => "PR", "26" => "PE", "22" => "PI", "33" => "RJ", "24" => "RN",
        "43" => "RS", "11" => "RO", "14" => "RR", "42" => "SC", "35" => "SP",
        "28" => "SE", "17" => "TO", _ => null
    };
}
