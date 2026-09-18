using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Infrastructure.Services;

namespace Sistema.API.Controllers.Estoque;

/// <summary>
/// OAuth do Mercado Livre para consultar preço de mercado online (usado no
/// comparativo por nome). Fluxo: /autorizar → usuário loga no ML → /callback
/// recebe o code e salva o token.
/// </summary>
[ApiController]
[Route("api/mercadolivre")]
public class MercadoLivreController(MercadoLivreService ml) : ControllerBase
{
    [HttpGet("status")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var conectado = ml.Configurado && await ml.ConectadoAsync(ct);
        // Testa a busca: o ML bloqueia (403) a busca de catálogo p/ terceiros mesmo autenticado.
        bool buscaOk = false;
        if (conectado)
        {
            try { var (s, _) = await ml.TestarBuscaAsync("aveia", ct); buscaOk = s == 200; }
            catch { buscaOk = false; }
        }
        return Ok(new { configurado = ml.Configurado, conectado, buscaOk });
    }

    /// <summary>Devolve a URL de autorização do ML (o frontend abre numa nova aba).</summary>
    [HttpGet("autorizar")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro")]
    public IActionResult Autorizar()
    {
        if (!ml.Configurado)
            return BadRequest(new { mensagem = "Mercado Livre não configurado (ClientId/ClientSecret) no servidor." });
        return Ok(new { url = ml.UrlAutorizacao() });
    }

    [HttpGet("testar")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro")]
    public async Task<IActionResult> Testar([FromQuery] string q = "aveia", CancellationToken ct = default)
    {
        var (status, corpo) = await ml.TestarBuscaAsync(q, ct);
        return Ok(new { status, corpo });
    }

    /// <summary>Diagnóstico: testa vários endpoints de busca/catálogo e mostra o status de cada um.</summary>
    [HttpGet("diagnostico")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro")]
    public async Task<IActionResult> Diagnostico([FromQuery] string q = "aveia", CancellationToken ct = default)
    {
        var r = await ml.DiagnosticoAsync(q, ct);
        return Ok(r.Select(x => new { endpoint = x.nome, status = x.status, corpo = x.corpo }));
    }

    /// <summary>
    /// Referência de catálogo do ML por nome ou EAN (nome canônico + marca + GTIN + foto).
    /// NÃO é preço — o ML bloqueia preço p/ terceiros. Serve p/ enriquecer o cadastro.
    /// </summary>
    [HttpGet("catalogo")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro,Estoquista")]
    public async Task<IActionResult> Catalogo([FromQuery] string q, [FromQuery] bool ean = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new { conectado = false, itens = Array.Empty<object>() });
        if (!ml.Configurado || !await ml.ConectadoAsync(ct))
            return Ok(new { conectado = false, itens = Array.Empty<object>() });
        var itens = await ml.BuscarCatalogoAsync(q.Trim(), ean, 6, ct);
        return Ok(new { conectado = true, itens });
    }

    /// <summary>Callback do ML (browser é redirecionado aqui com ?code=). Público.</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state,
        [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code))
            return Content(Pagina("Autorização não concluída. Você pode fechar esta aba e tentar de novo."), "text/html");
        try
        {
            await ml.TrocarCodigoAsync(code, state, ct);
            return Content(Pagina("✅ Mercado Livre conectado! Pode fechar esta aba e voltar ao sistema."), "text/html");
        }
        catch (Exception ex)
        {
            return Content(Pagina("Falha ao conectar: " + System.Net.WebUtility.HtmlEncode(ex.Message)), "text/html");
        }
    }

    private static string Pagina(string msg) =>
        $"<!doctype html><html lang=pt-br><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>" +
        "<title>Mercado Livre</title></head><body style='font-family:system-ui;display:flex;min-height:100vh;align-items:center;justify-content:center;background:#faf7f2;margin:0'>" +
        $"<div style='max-width:420px;text-align:center;padding:24px;background:#fff;border-radius:16px;box-shadow:0 2px 12px rgba(0,0,0,.08)'>{msg}</div></body></html>";
}
