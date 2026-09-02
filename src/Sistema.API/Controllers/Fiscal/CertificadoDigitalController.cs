using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;
using System.Security.Cryptography.X509Certificates;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/certificado")]
[Authorize(Roles = "Administrador,Contador")]   // certificado digital A1 é a identidade fiscal — só admin/contador
public class CertificadoDigitalController(SistemaDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private string CertDir => Path.Combine(env.ContentRootPath, "certificados");

    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var config = await db.ConfiguracoesFiscais.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        if (config?.CertificadoPfxBase64 is null)
            return Ok(null);

        try
        {
            var bytes = Convert.FromBase64String(config.CertificadoPfxBase64);
            var cert = new X509Certificate2(bytes, config.CertificadoSenha,
                X509KeyStorageFlags.EphemeralKeySet);

            var diasRestantes = (cert.NotAfter - DateTime.Now).Days;
            return Ok(new
            {
                instalado = true,
                razaoSocial = cert.GetNameInfo(X509NameType.SimpleName, false),
                cnpj = ExtrairCnpjDoCert(cert),
                validoAte = cert.NotAfter,
                diasRestantes,
                expirado = diasRestantes < 0,
                expirando = diasRestantes >= 0 && diasRestantes <= 30,
                emissor = cert.Issuer,
            });
        }
        catch
        {
            return Ok(null);
        }
    }

    [HttpPost("validar")]
    public IActionResult Validar([FromForm] CertificadoUploadRequest req)
    {
        if (req.Arquivo == null || req.Arquivo.Length == 0)
            return BadRequest(new { title = "Arquivo não enviado." });

        try
        {
            using var ms = new MemoryStream();
            req.Arquivo.CopyTo(ms);
            var bytes = ms.ToArray();
            var cert = new X509Certificate2(bytes, req.Senha, X509KeyStorageFlags.EphemeralKeySet);

            var diasRestantes = (cert.NotAfter - DateTime.Now).Days;
            return Ok(new
            {
                valido = true,
                razaoSocial = cert.GetNameInfo(X509NameType.SimpleName, false),
                cnpj = ExtrairCnpjDoCert(cert),
                validoAte = cert.NotAfter,
                diasRestantes,
                expirado = diasRestantes < 0,
                emissor = cert.Issuer,
            });
        }
        catch
        {
            return BadRequest(new { title = "Senha incorreta ou arquivo corrompido." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Instalar([FromForm] CertificadoUploadRequest req, CancellationToken ct)
    {
        if (req.Arquivo == null || req.Arquivo.Length == 0)
            return BadRequest(new { title = "Arquivo não enviado." });

        using var ms = new MemoryStream();
        await req.Arquivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        try
        {
            var cert = new X509Certificate2(bytes, req.Senha, X509KeyStorageFlags.EphemeralKeySet);
            if (cert.NotAfter < DateTime.Now)
                return BadRequest(new { title = "O certificado está expirado." });
        }
        catch
        {
            return BadRequest(new { title = "Senha incorreta ou arquivo corrompido." });
        }

        var config = await db.ConfiguracoesFiscais
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);

        var b64 = Convert.ToBase64String(bytes);

        if (config is null)
        {
            return BadRequest(new { title = "Configure os dados fiscais antes de instalar o certificado." });
        }

        config.CertificadoPfxBase64 = b64;
        config.CertificadoSenha = req.Senha;
        await db.SaveChangesAsync(ct);

        return Ok(new { mensagem = "Certificado instalado com sucesso!" });
    }

    private static string? ExtrairCnpjDoCert(X509Certificate2 cert)
    {
        var subject = cert.Subject;
        var match = System.Text.RegularExpressions.Regex.Match(subject, @"\d{14}");
        return match.Success ? match.Value : null;
    }
}

public class CertificadoUploadRequest
{
    public IFormFile? Arquivo { get; set; }
    public string Senha { get; set; } = "";
    public Guid EmpresaId { get; set; }
}
