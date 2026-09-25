using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Infrastructure.Branding;

namespace Sistema.API.Controllers;

/// <summary>
/// Expõe a marca da instância para o frontend (nome, logo, cores, links). Público (sem login):
/// o front precisa dela já na tela de login. Cada instância responde com a sua própria marca.
/// </summary>
[ApiController]
[Route("api/branding")]
[AllowAnonymous]
public class BrandingController(BrandingOptions branding) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        nome = branding.Nome,
        slogan = branding.Slogan,
        logoUrl = branding.LogoUrl,
        faviconUrl = string.IsNullOrWhiteSpace(branding.FaviconUrl) ? branding.LogoUrl : branding.FaviconUrl,
        siteUrl = branding.SiteUrl,
        publicBaseUrl = branding.PublicBaseUrl,
        catalogoProdutoUrl = branding.CatalogoProdutoUrl,
        emailContato = branding.EmailContato,
        corPrimaria = branding.CorPrimaria,
        corSecundaria = branding.CorSecundaria,
        corAccent = branding.CorAccent,
        corFundo = branding.CorFundo,
        autoCadastro = branding.AutoCadastro,
    });
}
