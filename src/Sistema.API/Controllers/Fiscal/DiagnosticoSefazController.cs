using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/diagnostico")]
[Authorize]
public class DiagnosticoSefazController(SistemaDbContext db) : ControllerBase
{
    private const string UrlProducao    = "https://www1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx";
    private const string UrlHomologacao = "https://hom1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx";
    private const string ActionUri      = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe/nfeDistDFeInt";

    [HttpGet]
    public async Task<IActionResult> Testar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var config = await db.ConfiguracoesFiscais.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        if (config?.CertificadoPfxBase64 is null)
            return Ok(new { erro = "Certificado não instalado" });

        var bytes = Convert.FromBase64String(config.CertificadoPfxBase64);
        X509Certificate2 cert;
        try
        {
            cert = new X509Certificate2(bytes, config.CertificadoSenha,
                X509KeyStorageFlags.Exportable);
        }
        catch (Exception ex)
        {
            return Ok(new { erro = $"Falha ao carregar certificado: {ex.Message}" });
        }

        var certInfo = new
        {
            sujeito     = cert.Subject,
            emissor     = cert.Issuer,
            validoAte   = cert.NotAfter,
            thumbprint  = cert.Thumbprint,
            temChavePriv = cert.HasPrivateKey,
        };

        var url = config.Ambiente == Domain.Fiscal.Entities.AmbienteFiscal.Producao
            ? UrlProducao : UrlHomologacao;

        // Tenta buscar o WSDL
        string? wsdlSnippet = null;
        try
        {
            var wh = new HttpClientHandler();
            wh.ClientCertificates.Add(cert);
            wh.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            using var wc = new HttpClient(wh) { Timeout = TimeSpan.FromSeconds(10) };
            var wsdl = await wc.GetStringAsync(url + "?wsdl", ct);
            // Extrai trechos com "action"
            var lines = wsdl.Split('\n')
                .Where(l => l.Contains("action", StringComparison.OrdinalIgnoreCase)
                         || l.Contains("operation", StringComparison.OrdinalIgnoreCase))
                .Take(20);
            wsdlSnippet = string.Join('\n', lines);
        }
        catch (Exception ex)
        {
            wsdlSnippet = $"Falha ao buscar WSDL: {ex.Message}";
        }

        const string actionAntiga = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe/nfeDistDFeInt";
        const string actionNova   = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe/nfeDistDFeInteresse";

        var soap12Nova = await TestarSoap12(url, cert, actionNova, ct);
        var curlResult = await TestarComCurl(url, cert, actionNova, ct);

        return Ok(new
        {
            url,
            certInfo,
            wsdlSnippet,
            soap12ActionNova = soap12Nova,
            curlResult,
        });
    }

    private static async Task<object> TestarSoap12(string url, X509Certificate2 cert, string action, CancellationToken ct)
    {
        var envelope = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap12:Envelope xmlns:soap12="http://www.w3.org/2003/05/soap-envelope">
              <soap12:Header>
                <nfeCabecMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe">
                  <cUF>35</cUF><versaoDados>1.01</versaoDados>
                </nfeCabecMsg>
              </soap12:Header>
              <soap12:Body>
                <nfeDadosMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe">
                  <distDFeInt versao="1.01" xmlns="http://www.portalfiscal.inf.br/nfe">
                    <tpAmb>2</tpAmb><cUFAutor>35</cUFAutor>
                    <CNPJ>99999999000191</CNPJ>
                    <distNSU><ultNSU>000000000000000</ultNSU></distNSU>
                  </distDFeInt>
                </nfeDadosMsg>
              </soap12:Body>
            </soap12:Envelope>
            """;
        try
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(cert);
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };

            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(envelope));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/soap+xml") { CharSet = "utf-8" };
            content.Headers.ContentType.Parameters.Add(
                new NameValueHeaderValue("action", $"\"{action}\""));

            var resp = await client.PostAsync(url, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new { status = (int)resp.StatusCode, actionUsada = action, resposta = body[..Math.Min(600, body.Length)] };
        }
        catch (Exception ex) { return new { erro = ex.Message }; }
    }

    private static async Task<object> TestarComCurl(string url, X509Certificate2 cert, string action, CancellationToken ct)
    {
        var pfxPath  = $"/tmp/diag_{Guid.NewGuid():N}.pfx";
        var soapPath = $"/tmp/soap_{Guid.NewGuid():N}.xml";
        try
        {
            var pfxBytes = cert.Export(X509ContentType.Pfx, "diag123");
            await System.IO.File.WriteAllBytesAsync(pfxPath, pfxBytes, ct);

            var soap = $"""<?xml version="1.0" encoding="utf-8"?><soap12:Envelope xmlns:soap12="http://www.w3.org/2003/05/soap-envelope"><soap12:Header><nfeCabecMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe"><cUF>91</cUF><versaoDados>1.01</versaoDados></nfeCabecMsg></soap12:Header><soap12:Body><nfeDadosMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe"><distDFeInt versao="1.01" xmlns="http://www.portalfiscal.inf.br/nfe"><tpAmb>1</tpAmb><cUFAutor>35</cUFAutor><CNPJ>{new string(cert.Subject.Where(char.IsDigit).Take(14).ToArray())}</CNPJ><distNSU><ultNSU>000000000000000</ultNSU></distNSU></distDFeInt></nfeDadosMsg></soap12:Body></soap12:Envelope>""";
            await System.IO.File.WriteAllTextAsync(soapPath, soap, ct);

            var args = $"-sk --cert-type P12 --cert \"{pfxPath}:diag123\" " +
                       $"-H \"Content-Type: application/soap+xml;charset=utf-8;action=\\\"{action}\\\"\" " +
                       $"--data @\"{soapPath}\" \"{url}\"";

            var psi = new System.Diagnostics.ProcessStartInfo("curl", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            return new { exitCode = proc.ExitCode, stdout = stdout[..Math.Min(600, stdout.Length)], stderr };
        }
        catch (Exception ex) { return new { erro = ex.Message }; }
        finally
        {
            try { System.IO.File.Delete(pfxPath); } catch { }
            try { System.IO.File.Delete(soapPath); } catch { }
        }
    }

    private static async Task<object> TestarSoap11(string url, X509Certificate2 cert, string? action, CancellationToken ct)
    {
        var envelope = """
            <?xml version="1.0" encoding="utf-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
              <soapenv:Header>
                <nfeCabecMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe">
                  <cUF>35</cUF><versaoDados>1.01</versaoDados>
                </nfeCabecMsg>
              </soapenv:Header>
              <soapenv:Body>
                <nfeDadosMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe">
                  <distDFeInt versao="1.01" xmlns="http://www.portalfiscal.inf.br/nfe">
                    <tpAmb>2</tpAmb><cUFAutor>35</cUFAutor>
                    <CNPJ>99999999000191</CNPJ>
                    <distNSU><ultNSU>000000000000000</ultNSU></distNSU>
                  </distDFeInt>
                </nfeDadosMsg>
              </soapenv:Body>
            </soapenv:Envelope>
            """;
        try
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(cert);
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            if (action is not null)
                client.DefaultRequestHeaders.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(envelope));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

            var resp = await client.PostAsync(url, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new { status = (int)resp.StatusCode, actionUsada = action ?? "(nenhuma)", resposta = body[..Math.Min(500, body.Length)] };
        }
        catch (Exception ex) { return new { erro = ex.Message }; }
    }
}
