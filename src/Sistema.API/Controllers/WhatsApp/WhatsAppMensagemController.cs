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
    public async Task<IActionResult> ObterConfig([FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.LocalEstoqueId == localEstoqueId, ct)
            ?? ConfiguracaoWhatsAppMensagem.Criar(empresaId, localEstoqueId);

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
        [FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, [FromBody] SalvarConfigWhatsAppRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.LocalEstoqueId == localEstoqueId, ct);

        if (cfg is null)
        {
            cfg = ConfiguracaoWhatsAppMensagem.Criar(empresaId, localEstoqueId);
            db.ConfiguracoesWhatsAppMensagem.Add(cfg);
        }

        // Só atualiza o token se um novo valor foi enviado (não começa com ****)
        var token = req.AccessToken?.StartsWith("****") == true
            ? (await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
               .Where(c => c.EmpresaId == empresaId && c.LocalEstoqueId == localEstoqueId)
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
    public async Task<IActionResult> ObterConfigCatalogo([FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.LocalEstoqueId == localEstoqueId, ct)
            ?? ConfiguracaoWhatsAppMensagem.Criar(empresaId, localEstoqueId);

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
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId && c.LocalEstoqueId == req.LocalEstoqueId, ct);
        if (cfg is null)
        {
            cfg = ConfiguracaoWhatsAppMensagem.Criar(req.EmpresaId, req.LocalEstoqueId);
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

    /// <summary>Remove o template cadastrado (só do sistema — não afeta a Meta).</summary>
    [HttpDelete("templates/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> ExcluirTemplate(Guid id, CancellationToken ct)
    {
        var t = await db.TemplatesWhatsAppMensagem.FindAsync([id], ct);
        if (t is null) return NotFound();
        db.TemplatesWhatsAppMensagem.Remove(t);
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

    /// <summary>Cria um template de promoção padrão na Meta e envia para análise.
    /// Usa a arte mais recente como imagem de exemplo do cabeçalho, se houver.</summary>
    [HttpPost("templates/criar-promocao")]
    [Authorize]
    public async Task<IActionResult> CriarTemplatePromocao(
        [FromBody] CriarTemplatePromocaoRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);
        if (cfg?.BusinessAccountId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "Configure o Business Account ID e o Access Token primeiro." });

        // Imagem de exemplo do cabeçalho: usa uma arte já gerada (PNG), se existir.
        byte[]? imagemExemplo = null;
        var arte = await db.ArtesMarketing.AsNoTracking()
            .Where(a => a.EmpresaId == req.EmpresaId && a.UrlExportada != null)
            .OrderByDescending(a => a.CriadoEm)
            .FirstOrDefaultAsync(ct);
        if (arte?.UrlExportada is { } url)
        {
            var caminho = Path.Combine("wwwroot", url.TrimStart('/'));
            if (System.IO.File.Exists(caminho))
                imagemExemplo = await System.IO.File.ReadAllBytesAsync(caminho, ct);
        }

        // Corpo padrão: {{1}} nome, {{2}} desconto, {{3}} produto, {{4}} de, {{5}} por.
        const string corpo =
            "🌿 Oferta EcoGranel!\n\n" +
            "Olá {{1}}, aproveite {{2}} em {{3}}.\n" +
            "De {{4}} por {{5}}.\n\n" +
            "Válido por tempo limitado. Passe na loja ou responda aqui! 🛒";
        var exemplos = new[] { "João", "10% de desconto", "Stévia Choco", "R$ 10,36", "R$ 9,32" };
        var nome = string.IsNullOrWhiteSpace(req.Nome) ? "promocao_produto" : req.Nome!.Trim().ToLowerInvariant();

        var (ok, status, erro) = await whatsAppService.CriarTemplateAsync(
            cfg.BusinessAccountId, cfg.AccessToken, cfg.AppId,
            nome, corpo, exemplos, imagemExemplo, "MARKETING", ct);

        if (!ok)
            return StatusCode(502, new { mensagem = $"A Meta recusou a criação: {erro}" });

        return Ok(new { nome, status, comImagem = imagemExemplo != null });
    }

    /// <summary>Cria um template PERSONALIZADO na Meta e envia para análise.</summary>
    [HttpPost("templates/criar-custom")]
    [Authorize]
    public async Task<IActionResult> CriarTemplateCustom(
        [FromBody] CriarTemplateCustomRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);
        if (cfg?.BusinessAccountId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "Configure o Business Account ID e o Access Token primeiro." });
        if (string.IsNullOrWhiteSpace(req.Nome) || string.IsNullOrWhiteSpace(req.Corpo))
            return BadRequest(new { mensagem = "Informe o nome e o corpo do template." });

        var nome = req.Nome.Trim().ToLowerInvariant().Replace(' ', '_');
        var categoria = string.IsNullOrWhiteSpace(req.Categoria) ? "UTILITY" : req.Categoria.ToUpperInvariant();

        // Imagem de exemplo (opcional): usa uma arte já gerada, se pedido.
        byte[]? imagem = null;
        if (req.ComImagem)
        {
            var arte = await db.ArtesMarketing.AsNoTracking()
                .Where(a => a.EmpresaId == req.EmpresaId && a.UrlExportada != null)
                .OrderByDescending(a => a.CriadoEm).FirstOrDefaultAsync(ct);
            if (arte?.UrlExportada is { } url)
            {
                var caminho = Path.Combine("wwwroot", url.TrimStart('/'));
                if (System.IO.File.Exists(caminho)) imagem = await System.IO.File.ReadAllBytesAsync(caminho, ct);
            }
        }

        var (ok, status, erro) = await whatsAppService.CriarTemplateAsync(
            cfg.BusinessAccountId, cfg.AccessToken, cfg.AppId,
            nome, req.Corpo, (req.Exemplos ?? []).ToList(), imagem, categoria, ct);

        if (!ok)
            return StatusCode(502, new { mensagem = $"A Meta recusou a criação: {erro}" });

        // Cadastra localmente (aparece na aba Templates com o selo de status "Em análise").
        var jaExiste = await db.TemplatesWhatsAppMensagem
            .AnyAsync(t => t.EmpresaId == req.EmpresaId && t.NomeMeta == nome, ct);
        if (!jaExiste)
        {
            db.TemplatesWhatsAppMensagem.Add(TemplateWhatsAppMensagem.Criar(
                req.EmpresaId, nome, TipoDisparoWhatsApp.Personalizado, "pt_BR", null, req.Corpo));
            await uow.SalvarAsync(ct);
        }

        return Ok(new { nome, status, comImagem = imagem != null });
    }

    /// <summary>
    /// Cria um template na Meta usando uma ARTE específica como imagem do cabeçalho
    /// e o vincula a um Tipo de Disparo (Aniversário/Promoção/…). Registra o template
    /// local com esse tipo e ativo, para o disparo automático das 8h usá-lo.
    /// </summary>
    [HttpPost("templates/criar-de-arte")]
    [Authorize]
    public async Task<IActionResult> CriarTemplateDeArte(
        [FromBody] CriarTemplateDeArteRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);
        if (cfg?.BusinessAccountId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "Configure o Business Account ID e o Access Token primeiro." });
        if (string.IsNullOrWhiteSpace(req.Nome) || string.IsNullOrWhiteSpace(req.Corpo))
            return BadRequest(new { mensagem = "Informe o nome e o corpo do template." });

        // Imagem do cabeçalho = a arte escolhida.
        byte[]? imagem = null;
        var arte = await db.ArtesMarketing.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == req.ArteId && a.EmpresaId == req.EmpresaId, ct);
        if (arte?.UrlExportada is { } url)
        {
            var caminho = Path.Combine("wwwroot", url.TrimStart('/'));
            if (System.IO.File.Exists(caminho)) imagem = await System.IO.File.ReadAllBytesAsync(caminho, ct);
        }
        if (imagem is null)
            return BadRequest(new { mensagem = "A arte não tem imagem exportada. Gere ou suba a imagem primeiro." });

        var nome = req.Nome.Trim().ToLowerInvariant().Replace(' ', '_');
        var tipoDisparo = Enum.TryParse<TipoDisparoWhatsApp>(req.TipoDisparo, out var td)
            ? td : TipoDisparoWhatsApp.Personalizado;
        // Categoria Meta: Aniversário/Novidade/Promoção são MARKETING; demais UTILITY.
        var categoria = tipoDisparo is TipoDisparoWhatsApp.Aniversario
            or TipoDisparoWhatsApp.Promocao or TipoDisparoWhatsApp.Novidade
            ? "MARKETING" : "UTILITY";

        var (ok, status, erro) = await whatsAppService.CriarTemplateAsync(
            cfg.BusinessAccountId, cfg.AccessToken, cfg.AppId,
            nome, req.Corpo, (req.Exemplos ?? []).ToList(), imagem, categoria, ct);

        if (!ok)
            return StatusCode(502, new { mensagem = $"A Meta recusou a criação: {erro}" });

        // Registra o template local com o tipo escolhido. Para os tipos automáticos
        // (Aniversário/Promoção/Novidade) só pode haver um ativo por tipo — desativa os demais.
        var jaExiste = await db.TemplatesWhatsAppMensagem
            .FirstOrDefaultAsync(t => t.EmpresaId == req.EmpresaId && t.NomeMeta == nome, ct);

        if (tipoDisparo is TipoDisparoWhatsApp.Aniversario
            or TipoDisparoWhatsApp.Promocao or TipoDisparoWhatsApp.Novidade)
        {
            var irmaos = await db.TemplatesWhatsAppMensagem
                .Where(t => t.EmpresaId == req.EmpresaId && t.TipoDisparo == tipoDisparo && t.Ativo)
                .ToListAsync(ct);
            foreach (var s in irmaos.Where(s => s.NomeMeta != nome)) s.Desativar();
        }

        if (jaExiste is null)
        {
            db.TemplatesWhatsAppMensagem.Add(TemplateWhatsAppMensagem.Criar(
                req.EmpresaId, nome, tipoDisparo, "pt_BR", req.VariaveisJson, req.Corpo));
        }
        else
        {
            jaExiste.Ativar();
            jaExiste.Atualizar(nome, "pt_BR", req.VariaveisJson, req.Corpo);
        }
        await uow.SalvarAsync(ct);

        return Ok(new { nome, status, tipoDisparo = tipoDisparo.ToString() });
    }

    // ─── Envio manual ─────────────────────────────────────────────────────────

    [HttpPost("enviar")]
    [Authorize]
    public async Task<IActionResult> EnviarManual(
        [FromBody] EnviarMensagemRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId && c.LocalEstoqueId == req.LocalEstoqueId, ct);

        if (cfg is null || !cfg.Ativo || cfg.PhoneNumberId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "WhatsApp não configurado ou inativo." });

        var tipo = Enum.TryParse<TipoDisparoWhatsApp>(req.TipoDisparo, out var t)
            ? t : TipoDisparoWhatsApp.Personalizado;

        var historico = HistoricoMensagemWhatsApp.Criar(
            req.EmpresaId, req.ClienteId, req.Telefone, req.NomeDestinatario,
            tipo, req.TemplateName, req.LocalEstoqueId);

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
    public IActionResult DispararPromocaoAgora([FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId)
    {
        Hangfire.BackgroundJob.Enqueue<Sistema.Infrastructure.Jobs.WhatsAppDisparoJob>(
            j => j.DispararPromocaoManualAsync(empresaId, localEstoqueId));
        return Accepted(new { mensagem = "Disparo de promoção enfileirado. Aguarde alguns instantes." });
    }

    /// <summary>Enfileira imediatamente o job de novidades para esta empresa.</summary>
    [HttpPost("disparar-novidade")]
    [Authorize]
    public IActionResult DispararNovidadeAgora([FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId)
    {
        Hangfire.BackgroundJob.Enqueue<Sistema.Infrastructure.Jobs.WhatsAppDisparoJob>(
            j => j.DispararNovidadeManualAsync(empresaId, localEstoqueId));
        return Accepted(new { mensagem = "Disparo de novidade enfileirado. Aguarde alguns instantes." });
    }

    // ─── Histórico ────────────────────────────────────────────────────────────

    [HttpGet("historico")]
    [Authorize]
    public async Task<IActionResult> Historico(
        [FromQuery] Guid empresaId,
        [FromQuery] Guid? localEstoqueId,
        [FromQuery] string? tipo,
        [FromQuery] string? status,
        [FromQuery] int pagina = 1,
        CancellationToken ct = default)
    {
        var query = db.HistoricosMensagensWhatsApp.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId
                     && (localEstoqueId == null || h.LocalEstoqueId == localEstoqueId));

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

    // ─── Caixa de entrada (conversas) ─────────────────────────────────────────

    /// <summary>Lista as conversas (agrupadas por telefone) com última mensagem, não-lidas e
    /// se a janela de 24h (para responder com texto livre) ainda está aberta.</summary>
    [HttpGet("/api/whatsapp/conversas")]
    [Authorize]
    public async Task<IActionResult> Conversas([FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, [FromQuery] string? busca, CancellationToken ct)
    {
        var brutas = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId
                     && (localEstoqueId == null || m.LocalEstoqueId == localEstoqueId))
            .GroupBy(m => m.Telefone)
            .Select(g => new
            {
                telefone = g.Key,
                nome = g.OrderByDescending(x => x.DataHora).Select(x => x.NomeContato).FirstOrDefault(),
                ultima = g.OrderByDescending(x => x.DataHora).FirstOrDefault(),
                ultimaData = g.Max(x => x.DataHora),
                naoLidas = g.Count(x => x.Direcao == DirecaoMensagemWhatsApp.Recebida && !x.Lida),
                ultimaRecebida = g.Where(x => x.Direcao == DirecaoMensagemWhatsApp.Recebida)
                    .Max(x => (DateTime?)x.DataHora),
            })
            .OrderByDescending(c => c.ultimaData)
            .ToListAsync(ct);

        var limite = DateTime.UtcNow.AddHours(-24);
        var conversas = brutas
            .Where(c => string.IsNullOrWhiteSpace(busca)
                || (c.nome ?? "").Contains(busca, StringComparison.OrdinalIgnoreCase)
                || c.telefone.Contains(busca))
            .Select(c => new
            {
                c.telefone, c.nome, c.ultimaData, c.naoLidas,
                ultimaMensagem = c.ultima!.Tipo == "text" || c.ultima.Texto.Length > 0
                    ? c.ultima.Texto : $"[{c.ultima.Tipo}]",
                dentroDe24h = c.ultimaRecebida.HasValue && c.ultimaRecebida.Value >= limite,
            });
        return Ok(conversas);
    }

    /// <summary>Mensagens de uma conversa (marca as recebidas como lidas).</summary>
    [HttpGet("/api/whatsapp/conversas/{telefone}/mensagens")]
    [Authorize]
    public async Task<IActionResult> MensagensConversa(string telefone, [FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var msgs = await db.MensagensWhatsApp
            .Where(m => m.EmpresaId == empresaId && m.Telefone == telefone
                     && (localEstoqueId == null || m.LocalEstoqueId == localEstoqueId))
            .OrderBy(m => m.DataHora)
            .ToListAsync(ct);

        var novas = msgs.Where(m => m.Direcao == DirecaoMensagemWhatsApp.Recebida && !m.Lida).ToList();
        if (novas.Count > 0)
        {
            foreach (var m in novas) m.MarcarLida();
            await uow.SalvarAsync(ct);
        }

        return Ok(msgs.Select(m => new
        {
            m.Id, m.Texto, m.Tipo, m.NomeContato, m.DataHora,
            m.MidiaUrl, m.MidiaMime, m.MidiaNome,
            direcao = m.Direcao.ToString(),
            status = m.StatusEntrega?.ToString(),
        }));
    }

    /// <summary>Responde ao cliente com texto livre (mensagem de sessão, janela de 24h).</summary>
    [HttpPost("/api/whatsapp/conversas/{telefone}/responder")]
    [Authorize]
    public async Task<IActionResult> Responder(string telefone, [FromBody] ResponderRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId && c.LocalEstoqueId == req.LocalEstoqueId, ct);
        if (cfg?.PhoneNumberId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "WhatsApp não configurado." });
        if (string.IsNullOrWhiteSpace(req.Texto))
            return BadRequest(new { mensagem = "Digite a mensagem." });

        var (sucesso, wamId, erro) = await whatsAppService.EnviarTexto(
            cfg.PhoneNumberId, cfg.AccessToken, telefone, req.Texto);
        if (!sucesso)
            return StatusCode(502, new { mensagem = $"Falha ao enviar: {erro}. " +
                "Fora da janela de 24h só é possível enviar template." });

        var nome = await db.MensagensWhatsApp
            .Where(m => m.Telefone == telefone && m.NomeContato != null)
            .Select(m => m.NomeContato).FirstOrDefaultAsync(ct);

        db.MensagensWhatsApp.Add(MensagemWhatsApp.Enviar(req.EmpresaId, telefone, nome, req.Texto, wamId, localEstoqueId: req.LocalEstoqueId));
        await uow.SalvarAsync(ct);
        return Ok(new { wamId });
    }

    /// <summary>Responde com uma mídia (foto/documento) anexada.</summary>
    [HttpPost("/api/whatsapp/conversas/{telefone}/responder-midia")]
    [Authorize]
    public async Task<IActionResult> ResponderMidia(
        string telefone, [FromForm] Guid empresaId, [FromForm] IFormFile arquivo,
        [FromForm] string? legenda, [FromForm] Guid? localEstoqueId, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.LocalEstoqueId == localEstoqueId, ct);
        if (cfg?.PhoneNumberId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "WhatsApp não configurado." });
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { mensagem = "Selecione um arquivo." });

        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var mime = arquivo.ContentType ?? "application/octet-stream";
        var tipo = mime.StartsWith("image/") ? "image"
                 : mime.StartsWith("audio/") ? "audio"
                 : mime.StartsWith("video/") ? "video" : "document";

        var (sucesso, wamId, erro) = await whatsAppService.EnviarMidia(
            cfg.PhoneNumberId, cfg.AccessToken, telefone, bytes, mime, tipo, legenda, arquivo.FileName);
        if (!sucesso)
            return StatusCode(502, new { mensagem = $"Falha ao enviar: {erro}" });

        // Salva uma cópia local para exibir na thread.
        var dir = Path.Combine("wwwroot", "uploads", "whatsapp");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(arquivo.FileName);
        var nomeArquivo = $"{Guid.NewGuid()}{ext}";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, nomeArquivo), bytes, ct);

        var nome = await db.MensagensWhatsApp
            .Where(m => m.Telefone == telefone && m.NomeContato != null)
            .Select(m => m.NomeContato).FirstOrDefaultAsync(ct);

        var msg = MensagemWhatsApp.Enviar(empresaId, telefone, nome, legenda ?? "", wamId, tipo, localEstoqueId);
        msg.DefinirMidia($"/uploads/whatsapp/{nomeArquivo}", mime, arquivo.FileName);
        db.MensagensWhatsApp.Add(msg);
        await uow.SalvarAsync(ct);
        return Ok(new { wamId });
    }

    /// <summary>Envia um template aprovado numa conversa (usado quando a janela de 24h expirou).</summary>
    [HttpPost("/api/whatsapp/conversas/{telefone}/responder-template")]
    [Authorize]
    public async Task<IActionResult> ResponderTemplate(
        string telefone, [FromBody] ResponderTemplateRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId && c.LocalEstoqueId == req.LocalEstoqueId, ct);
        if (cfg?.PhoneNumberId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "WhatsApp não configurado." });

        var (sucesso, wamId, erro) = await whatsAppService.EnviarTemplate(
            cfg.PhoneNumberId, cfg.AccessToken, telefone, req.TemplateName,
            req.Idioma ?? "pt_BR", req.Variaveis ?? []);
        if (!sucesso)
            return StatusCode(502, new { mensagem = $"Falha ao enviar template: {erro}" });

        var nome = req.Nome ?? await db.MensagensWhatsApp
            .Where(m => m.Telefone == telefone && m.NomeContato != null)
            .Select(m => m.NomeContato).FirstOrDefaultAsync(ct);
        db.MensagensWhatsApp.Add(MensagemWhatsApp.Enviar(
            req.EmpresaId, telefone, nome, $"📋 Template: {req.TemplateName}", wamId, localEstoqueId: req.LocalEstoqueId));
        await uow.SalvarAsync(ct);
        return Ok(new { wamId });
    }

    /// <summary>Envia a mensagem interativa de catálogo (catálogo conectado ao número).</summary>
    [HttpPost("/api/whatsapp/conversas/{telefone}/enviar-catalogo")]
    [Authorize]
    public async Task<IActionResult> EnviarCatalogo(
        string telefone, [FromBody] ResponderRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId && c.LocalEstoqueId == req.LocalEstoqueId, ct);
        if (cfg?.PhoneNumberId is null || cfg.AccessToken is null)
            return BadRequest(new { mensagem = "WhatsApp não configurado." });

        var body = string.IsNullOrWhiteSpace(req.Texto)
            ? "🌿 Confira nosso catálogo de produtos naturais! Toque em *Ver catálogo* abaixo."
            : req.Texto;

        // Sem thumbnail fixo: a Meta usa o primeiro produto do catálogo conectado.
        // (Requer que o catálogo tenha produtos aprovados/disponíveis para WhatsApp.)
        var (sucesso, wamId, erro) = await whatsAppService.EnviarCatalogo(
            cfg.PhoneNumberId, cfg.AccessToken, telefone, body, null);
        if (!sucesso)
            return StatusCode(502, new { mensagem = $"Falha ao enviar catálogo: {erro}. " +
                "Confirme que o catálogo está conectado ao número no WhatsApp Manager." });

        var nome = await db.MensagensWhatsApp
            .Where(m => m.Telefone == telefone && m.NomeContato != null)
            .Select(m => m.NomeContato).FirstOrDefaultAsync(ct);
        db.MensagensWhatsApp.Add(MensagemWhatsApp.Enviar(req.EmpresaId, telefone, nome, "📖 Catálogo enviado", wamId, localEstoqueId: req.LocalEstoqueId));
        await uow.SalvarAsync(ct);
        return Ok(new { wamId });
    }

    /// <summary>Total de mensagens recebidas não lidas (para o badge do menu).</summary>
    [HttpGet("/api/whatsapp/conversas/nao-lidas")]
    [Authorize]
    public async Task<IActionResult> NaoLidas([FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var total = await db.MensagensWhatsApp.CountAsync(
            m => m.EmpresaId == empresaId
              && (localEstoqueId == null || m.LocalEstoqueId == localEstoqueId)
              && m.Direcao == DirecaoMensagemWhatsApp.Recebida && !m.Lida, ct);
        return Ok(new { total });
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

                    // Status das mensagens que ENVIAMOS (entregue/lido/falhou).
                    if (value.TryGetProperty("statuses", out var statuses))
                    {
                        foreach (var s in statuses.EnumerateArray())
                        {
                            var wamId  = s.GetProperty("id").GetString();
                            var status = s.GetProperty("status").GetString();

                            var historico = await db.HistoricosMensagensWhatsApp
                                .FirstOrDefaultAsync(h => h.WamId == wamId, ct);
                            if (historico is not null)
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

                            // Também atualiza o tique das respostas da caixa de entrada.
                            var msgEnviada = await db.MensagensWhatsApp
                                .FirstOrDefaultAsync(x => x.WamId == wamId, ct);
                            if (msgEnviada is not null)
                                msgEnviada.AtualizarStatusEntrega(status switch
                                {
                                    "delivered" => StatusEntregaWhatsApp.Entregue,
                                    "read"      => StatusEntregaWhatsApp.Lida,
                                    "failed"    => StatusEntregaWhatsApp.Falhou,
                                    _           => StatusEntregaWhatsApp.Enviada,
                                });
                        }
                    }

                    // Mensagens que os clientes ENVIARAM (caixa de entrada).
                    if (value.TryGetProperty("messages", out var messages))
                    {
                        // Empresa + token do número que recebeu (pelo phone_number_id).
                        var phoneNumberId = value.TryGetProperty("metadata", out var meta)
                            && meta.TryGetProperty("phone_number_id", out var pnid)
                            ? pnid.GetString() : null;
                        var cfg = phoneNumberId is null ? null
                            : await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
                                .FirstOrDefaultAsync(c => c.PhoneNumberId == phoneNumberId, ct);
                        if (cfg is null) continue;
                        var empresaId = cfg.EmpresaId;
                        var localEstoqueId = cfg.LocalEstoqueId;

                        // Nome do contato (perfil do cliente).
                        string? nome = null;
                        if (value.TryGetProperty("contacts", out var contacts) && contacts.GetArrayLength() > 0
                            && contacts[0].TryGetProperty("profile", out var prof)
                            && prof.TryGetProperty("name", out var nm))
                            nome = nm.GetString();

                        foreach (var m in messages.EnumerateArray())
                        {
                            var wamId = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                            if (wamId != null && await db.MensagensWhatsApp.AnyAsync(x => x.WamId == wamId, ct))
                                continue;

                            var from = m.TryGetProperty("from", out var fromEl) ? fromEl.GetString() ?? "" : "";
                            var tipo = m.TryGetProperty("type", out var tpEl) ? tpEl.GetString() ?? "text" : "text";
                            var dataHora = m.TryGetProperty("timestamp", out var tsEl)
                                && long.TryParse(tsEl.GetString(), out var ts)
                                ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime
                                : DateTime.UtcNow;

                            var msg = MensagemWhatsApp.Receber(empresaId, from, nome, "", tipo, wamId, dataHora, localEstoqueId);

                            if (tipo == "text" && m.TryGetProperty("text", out var txtEl)
                                && txtEl.TryGetProperty("body", out var bodyEl))
                            {
                                msg = MensagemWhatsApp.Receber(empresaId, from, nome,
                                    bodyEl.GetString() ?? "", tipo, wamId, dataHora, localEstoqueId);
                            }
                            else if (m.TryGetProperty(tipo, out var midiaEl) && cfg.AccessToken is not null)
                            {
                                // Mídia: legenda + baixa o arquivo e salva em /uploads/whatsapp.
                                var legenda = midiaEl.TryGetProperty("caption", out var capEl) ? capEl.GetString() : null;
                                var nomeArq = midiaEl.TryGetProperty("filename", out var fnEl) ? fnEl.GetString() : null;
                                msg = MensagemWhatsApp.Receber(empresaId, from, nome,
                                    legenda ?? "", tipo, wamId, dataHora, localEstoqueId);

                                if (midiaEl.TryGetProperty("id", out var midIdEl) && midIdEl.GetString() is { } midId)
                                {
                                    var (bytes, mime) = await whatsAppService.BaixarMidia(midId, cfg.AccessToken);
                                    if (bytes is not null)
                                    {
                                        var dir = Path.Combine("wwwroot", "uploads", "whatsapp");
                                        Directory.CreateDirectory(dir);
                                        var ext = ExtensaoPorMime(mime, tipo);
                                        var arquivo = $"{Guid.NewGuid()}{ext}";
                                        await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, arquivo), bytes, ct);
                                        msg.DefinirMidia($"/uploads/whatsapp/{arquivo}", mime, nomeArq);
                                    }
                                }
                            }
                            else if (tipo != "text")
                            {
                                msg = MensagemWhatsApp.Receber(empresaId, from, nome, $"[{tipo}]", tipo, wamId, dataHora, localEstoqueId);
                            }

                            db.MensagensWhatsApp.Add(msg);
                        }
                    }
                }
            }
            await uow.SalvarAsync(ct);
        }
        catch { /* webhook nunca pode retornar erro para Meta */ }

        return Ok();
    }

    private static string ExtensaoPorMime(string? mime, string tipo)
    {
        var baseMime = (mime ?? "").Split(';')[0].Trim().ToLowerInvariant();
        return baseMime switch
        {
            "image/jpeg" => ".jpg",
            "image/png"  => ".png",
            "image/webp" => ".webp",
            "audio/ogg"  => ".ogg",
            "audio/mpeg" => ".mp3",
            "audio/mp4"  => ".m4a",
            "video/mp4"  => ".mp4",
            "application/pdf" => ".pdf",
            _ => tipo == "image" ? ".jpg" : tipo == "audio" ? ".ogg" : ".bin"
        };
    }
}

public record SalvarConfigWhatsAppRequest(
    string? PhoneNumberId, string? AccessToken, string? BusinessAccountId,
    string? WebhookVerifyToken, string? AppId, bool Ativo,
    bool EnviarAniversario, bool EnviarPromocoes, bool EnviarNovidades,
    int HoraDisparo = 8);

public record SalvarConfigCatalogoRequest(
    Guid EmpresaId, string? PhoneNumberId, string? AccessToken,
    string? CatalogId, string? NumeroWhatsApp, bool Ativo = false,
    Guid? LocalEstoqueId = null);

public record CriarTemplateWhatsAppRequest(
    string NomeMeta, string TipoDisparo, string? Idioma = "pt_BR",
    string? VariaveisJson = null, string? ExemploTexto = null);

public record EnviarMensagemRequest(
    Guid EmpresaId, string Telefone, string NomeDestinatario, string TemplateName,
    string TipoDisparo = "Personalizado", Guid? ClienteId = null,
    string? Idioma = "pt_BR", IEnumerable<string>? Variaveis = null,
    string? HeaderImageUrl = null, Guid? LocalEstoqueId = null);

public record CriarTemplatePromocaoRequest(Guid EmpresaId, string? Nome = null);

public record CriarTemplateCustomRequest(
    Guid EmpresaId, string Nome, string Corpo, string? Categoria = "UTILITY",
    IEnumerable<string>? Exemplos = null, bool ComImagem = false);

public record CriarTemplateDeArteRequest(
    Guid EmpresaId, Guid ArteId, string TipoDisparo, string Nome, string Corpo,
    IEnumerable<string>? Exemplos = null, string? VariaveisJson = null);

public record ResponderRequest(Guid EmpresaId, string Texto, Guid? LocalEstoqueId = null);

public record ResponderTemplateRequest(
    Guid EmpresaId, string TemplateName, string? Idioma = "pt_BR",
    IEnumerable<string>? Variaveis = null, string? Nome = null, Guid? LocalEstoqueId = null);
