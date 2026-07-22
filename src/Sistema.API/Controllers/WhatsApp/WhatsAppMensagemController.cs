using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;
using System.Text.Json;

namespace Sistema.API.Controllers.WhatsApp;

[ApiController]
[Route("api/whatsapp/mensagem")]
public class WhatsAppMensagemController(
    SistemaDbContext db,
    IUnitOfWork uow,
    WhatsAppCloudApiService whatsAppService) : ControllerBase
{
    // ─── Configuração ─────────────────────────────────────────────────────────

    [HttpGet("config")]
    [Authorize]
    public async Task<IActionResult> ObterConfig([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct)
            ?? ConfiguracaoWhatsAppMensagem.Criar(empresaId);

        return Ok(new
        {
            cfg.Id,
            cfg.PhoneNumberId,
            // AccessToken mascarado — só exibe últimos 6 caracteres
            AccessTokenMask = cfg.AccessToken is null ? null
                : "****" + cfg.AccessToken[^Math.Min(6, cfg.AccessToken.Length)..],
            cfg.BusinessAccountId,
            cfg.WebhookVerifyToken,
            cfg.AppId,
            cfg.Ativo,
            cfg.EnviarAniversario,
            cfg.EnviarPromocoes,
            cfg.EnviarNovidades,
            cfg.HoraDisparo,
        });
    }

    [HttpPut("config")]
    [Authorize]
    public async Task<IActionResult> SalvarConfig(
        [FromQuery] Guid empresaId, [FromBody] SalvarConfigWhatsAppRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        if (cfg is null)
        {
            cfg = ConfiguracaoWhatsAppMensagem.Criar(empresaId);
            db.ConfiguracoesWhatsAppMensagem.Add(cfg);
        }

        // Só atualiza o token se um novo valor foi enviado (não começa com ****)
        var token = req.AccessToken?.StartsWith("****") == true
            ? (await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
               .Where(c => c.EmpresaId == empresaId)
               .Select(c => c.AccessToken)
               .FirstOrDefaultAsync(ct))
            : req.AccessToken;

        cfg.Atualizar(req.PhoneNumberId, token, req.BusinessAccountId,
            req.WebhookVerifyToken, req.AppId,
            req.Ativo, req.EnviarAniversario, req.EnviarPromocoes, req.EnviarNovidades,
            req.HoraDisparo);

        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // ─── Configuração do Catálogo (bloco separado na tela) ────────────────────

    [HttpGet("/api/whatsapp/configuracao")]
    [Authorize]
    public async Task<IActionResult> ObterConfigCatalogo([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct)
            ?? ConfiguracaoWhatsAppMensagem.Criar(empresaId);

        return Ok(new
        {
            cfg.PhoneNumberId,
            AccessToken = cfg.AccessToken is null ? "" : "****" + cfg.AccessToken[^Math.Min(6, cfg.AccessToken.Length)..],
            cfg.CatalogId,
            cfg.NumeroWhatsApp,
            cfg.Ativo,
        });
    }

    [HttpPut("/api/whatsapp/configuracao")]
    [Authorize]
    public async Task<IActionResult> SalvarConfigCatalogo(
        [FromBody] SalvarConfigCatalogoRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);
        if (cfg is null)
        {
            cfg = ConfiguracaoWhatsAppMensagem.Criar(req.EmpresaId);
            db.ConfiguracoesWhatsAppMensagem.Add(cfg);
        }

        // Catálogo por feed: só grava CatalogId (informativo) e o número do link.
        // NÃO toca no Access Token nem no Phone Number ID (do bloco de mensagens).
        cfg.AtualizarCatalogo(req.CatalogId, req.NumeroWhatsApp);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // ─── Templates ────────────────────────────────────────────────────────────

    [HttpGet("templates")]
    [Authorize]
    public async Task<IActionResult> ListarTemplates([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.TemplatesWhatsAppMensagem.AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && t.Ativo)
            .OrderBy(t => t.TipoDisparo)
            .ToListAsync(ct));

    [HttpPost("templates")]
    [Authorize]
    public async Task<IActionResult> CriarTemplate(
        [FromQuery] Guid empresaId, [FromBody] CriarTemplateWhatsAppRequest req, CancellationToken ct)
    {
        var tipo = Enum.Parse<TipoDisparoWhatsApp>(req.TipoDisparo);
        var t    = TemplateWhatsAppMensagem.Criar(empresaId, req.NomeMeta, tipo,
            req.Idioma ?? "pt_BR", req.VariaveisJson, req.ExemploTexto);
        db.TemplatesWhatsAppMensagem.Add(t);
        await uow.SalvarAsync(ct);
        return Ok(new { t.Id });
    }

    [HttpPut("templates/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> AtualizarTemplate(Guid id, [FromBody] CriarTemplateWhatsAppRequest req, CancellationToken ct)
    {
        var t = await db.TemplatesWhatsAppMensagem.FindAsync([id], ct);
        if (t is null) return NotFound();
        t.Atualizar(req.NomeMeta, req.Idioma ?? "pt_BR", req.VariaveisJson, req.ExemploTexto);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Lista templates aprovados diretamente na Meta (requer config válida).</summary>
    [HttpGet("templates/meta")]
    [Authorize]
    public async Task<IActionResult> ListarTemplatesMeta([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        if (cfg?.BusinessAccountId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "Configure o Business Account ID e o Access Token primeiro." });

        var (templates, erro) = await whatsAppService.ListarTemplatesAprovados(
            cfg.BusinessAccountId, cfg.AccessToken);

        if (erro is not null)
            return StatusCode(502, new { mensagem = $"A Meta recusou a consulta: {erro}" });

        return Ok(templates);
    }

    // ─── Envio manual ─────────────────────────────────────────────────────────

    [HttpPost("enviar")]
    [Authorize]
    public async Task<IActionResult> EnviarManual(
        [FromBody] EnviarMensagemRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);

        if (cfg is null || !cfg.Ativo || cfg.PhoneNumberId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "WhatsApp não configurado ou inativo." });

        var tipo = Enum.TryParse<TipoDisparoWhatsApp>(req.TipoDisparo, out var t)
            ? t : TipoDisparoWhatsApp.Personalizado;

        var historico = HistoricoMensagemWhatsApp.Criar(
            req.EmpresaId, req.ClienteId, req.Telefone, req.NomeDestinatario,
            tipo, req.TemplateName);

        db.HistoricosMensagensWhatsApp.Add(historico);
        await uow.SalvarAsync(ct);

        var (sucesso, wamId, erro) = await whatsAppService.EnviarTemplate(
            cfg.PhoneNumberId, cfg.AccessToken,
            req.Telefone, req.TemplateName, req.Idioma ?? "pt_BR", req.Variaveis ?? [],
            req.HeaderImageUrl);

        if (sucesso) historico.MarcarEnviada(wamId!);
        else         historico.MarcarFalha(erro ?? "Erro desconhecido");

        await uow.SalvarAsync(ct);

        return sucesso
            ? Ok(new { mensagem = "Mensagem enviada com sucesso!", wamId })
            : StatusCode(502, new { mensagem = "Falha no envio.", erro });
    }

    // ─── Disparos manuais de campanha ─────────────────────────────────────────

    /// <summary>Enfileira imediatamente o job de promoções para esta empresa.</summary>
    [HttpPost("disparar-promocao")]
    [Authorize]
    public IActionResult DispararPromocaoAgora([FromQuery] Guid empresaId)
    {
        Hangfire.BackgroundJob.Enqueue<Sistema.Infrastructure.Jobs.WhatsAppDisparoJob>(
            j => j.DispararPromocaoManualAsync(empresaId));
        return Accepted(new { mensagem = "Disparo de promoção enfileirado. Aguarde alguns instantes." });
    }

    /// <summary>Enfileira imediatamente o job de novidades para esta empresa.</summary>
    [HttpPost("disparar-novidade")]
    [Authorize]
    public IActionResult DispararNovidadeAgora([FromQuery] Guid empresaId)
    {
        Hangfire.BackgroundJob.Enqueue<Sistema.Infrastructure.Jobs.WhatsAppDisparoJob>(
            j => j.DispararNovidadeManualAsync(empresaId));
        return Accepted(new { mensagem = "Disparo de novidade enfileirado. Aguarde alguns instantes." });
    }

    // ─── Histórico ────────────────────────────────────────────────────────────

    [HttpGet("historico")]
    [Authorize]
    public async Task<IActionResult> Historico(
        [FromQuery] Guid empresaId,
        [FromQuery] string? tipo,
        [FromQuery] string? status,
        [FromQuery] int pagina = 1,
        CancellationToken ct = default)
    {
        var query = db.HistoricosMensagensWhatsApp.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId);

        if (Enum.TryParse<TipoDisparoWhatsApp>(tipo, out var td))
            query = query.Where(h => h.TipoDisparo == td);

        if (Enum.TryParse<StatusMensagemWhatsApp>(status, out var st))
            query = query.Where(h => h.Status == st);

        var total = await query.CountAsync(ct);
        var itens = await query
            .OrderByDescending(h => h.EnviadoEm)
            .Skip((pagina - 1) * 50)
            .Take(50)
            .Select(h => new
            {
                h.Id, h.Telefone, h.NomeDestinatario,
                TipoDisparo = h.TipoDisparo.ToString(),
                h.TemplateName, h.WamId,
                Status    = h.Status.ToString(),
                h.ErroDetalhe,
                EnviadoEm = h.EnviadoEm.ToString("dd/MM/yyyy HH:mm"),
                h.EntregueEm, h.LidoEm,
            })
            .ToListAsync(ct);

        return Ok(new { total, pagina, itens });
    }

    // ─── Webhook Meta ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verificação do webhook pela Meta (GET).
    /// A Meta envia hub.mode=subscribe, hub.verify_token e hub.challenge.
    /// </summary>
    [HttpGet("/api/whatsapp/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookVerify(
        [FromQuery(Name = "hub.mode")]         string mode,
        [FromQuery(Name = "hub.verify_token")] string verifyToken,
        [FromQuery(Name = "hub.challenge")]    string challenge)
    {
        // Procura qualquer empresa com este verify token
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.WebhookVerifyToken == verifyToken && c.Ativo);

        if (cfg is null || mode != "subscribe")
            return Forbid();

        return Content(challenge, "text/plain");
    }

    /// <summary>
    /// Recebe notificações de status de mensagens da Meta (POST).
    /// Atualiza Entregue/Lido no histórico.
    /// </summary>
    [HttpPost("/api/whatsapp/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookReceber(CancellationToken ct)
    {
        string body;
        using (var sr = new System.IO.StreamReader(Request.Body))
            body = await sr.ReadToEndAsync(ct);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var entry = doc.RootElement.GetProperty("entry");
            foreach (var e in entry.EnumerateArray())
            {
                var changes = e.GetProperty("changes");
                foreach (var ch in changes.EnumerateArray())
                {
                    var value = ch.GetProperty("value");
                    if (!value.TryGetProperty("statuses", out var statuses)) continue;

                    foreach (var s in statuses.EnumerateArray())
                    {
                        var wamId  = s.GetProperty("id").GetString();
                        var status = s.GetProperty("status").GetString(); // sent/delivered/read/failed

                        var historico = await db.HistoricosMensagensWhatsApp
                            .FirstOrDefaultAsync(h => h.WamId == wamId, ct);

                        if (historico is null) continue;

                        switch (status)
                        {
                            case "delivered": historico.MarcarEntregue(); break;
                            case "read":      historico.MarcarLida();     break;
                            case "failed":
                                var erros = s.TryGetProperty("errors", out var errsEl)
                                    ? errsEl.GetRawText() : "erro Meta";
                                historico.MarcarFalha(erros);
                                break;
                        }
                    }
                }
            }
            await uow.SalvarAsync(ct);
        }
        catch { /* webhook nunca pode retornar erro para Meta */ }

        return Ok();
    }
}

public record SalvarConfigWhatsAppRequest(
    string? PhoneNumberId, string? AccessToken, string? BusinessAccountId,
    string? WebhookVerifyToken, string? AppId, bool Ativo,
    bool EnviarAniversario, bool EnviarPromocoes, bool EnviarNovidades,
    int HoraDisparo = 8);

public record SalvarConfigCatalogoRequest(
    Guid EmpresaId, string? PhoneNumberId, string? AccessToken,
    string? CatalogId, string? NumeroWhatsApp, bool Ativo = false);

public record CriarTemplateWhatsAppRequest(
    string NomeMeta, string TipoDisparo, string? Idioma = "pt_BR",
    string? VariaveisJson = null, string? ExemploTexto = null);

public record EnviarMensagemRequest(
    Guid EmpresaId, string Telefone, string NomeDestinatario, string TemplateName,
    string TipoDisparo = "Personalizado", Guid? ClienteId = null,
    string? Idioma = "pt_BR", IEnumerable<string>? Variaveis = null,
    string? HeaderImageUrl = null);
