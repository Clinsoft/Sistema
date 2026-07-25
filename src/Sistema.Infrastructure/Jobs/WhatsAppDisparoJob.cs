using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Marketing.Entities;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;
using System.Text.Json;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Executa disparos automáticos de WhatsApp:
/// aniversariantes, promoções (produtos com validade próxima) e novidades.
/// Registrado para rodar às 8h todo dia.
/// </summary>
public class WhatsAppDisparoJob(
    SistemaDbContext db,
    WhatsAppCloudApiService whatsApp,
    ILogger<WhatsAppDisparoJob> logger)
{
    [AutomaticRetry(Attempts = 2)]
    public async Task ExecutarAsync()
    {
        var empresas = await db.Empresas.AsNoTracking()
            .Where(e => e.Ativo)
            .Select(e => new { e.Id, e.NomeFantasia })
            .ToListAsync();

        foreach (var empresa in empresas)
        {
            var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
                .FirstOrDefaultAsync(c => c.EmpresaId == empresa.Id);

            if (cfg is null || !cfg.Ativo
                || string.IsNullOrEmpty(cfg.PhoneNumberId)
                || string.IsNullOrEmpty(cfg.AccessToken))
                continue;

            if (cfg.EnviarAniversario)
                await DispararAniversariantes(empresa.Id, empresa.NomeFantasia, cfg);

            if (cfg.EnviarPromocoes)
                await DispararPromocoes(empresa.Id, empresa.NomeFantasia, cfg);

            if (cfg.EnviarNovidades)
                await DispararNovidades(empresa.Id, empresa.NomeFantasia, cfg);
        }
    }

    // Variáveis comuns disponíveis em qualquer template.
    private const string LinkCatalogo = "https://ecogranel.com.br/produtos";
    private static Dictionary<string, string> VariaveisComuns(ClienteInfo c, string nomeEmpresa)
        => new()
        {
            ["nome_cliente"]     = c.Nome,
            ["primeiro_nome"]    = c.Nome.Split(' ')[0],
            ["nome_empresa"]     = nomeEmpresa,
            ["telefone"]         = c.Telefone,
            ["data_aniversario"] = c.DataNascimento?.ToString("dd/MM/yyyy") ?? "",
            ["link_catalogo"]    = LinkCatalogo,
        };

    // ─── Aniversariantes ─────────────────────────────────────────────────────

    private async Task DispararAniversariantes(Guid empresaId, string nomeEmpresa,
        ConfiguracaoWhatsAppMensagem cfg)
    {
        var template = await ObterTemplate(empresaId, TipoDisparoWhatsApp.Aniversario);
        if (template is null)
        {
            logger.LogWarning("[WhatsApp] {Empresa}: sem template de Aniversário configurado.", nomeEmpresa);
            return;
        }

        var hoje = DateTime.Today;
        var destinatarios = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId
                     && c.Ativo
                     && c.DataNascimento.HasValue
                     && c.DataNascimento.Value.Month == hoje.Month
                     && c.DataNascimento.Value.Day   == hoje.Day
                     && !string.IsNullOrEmpty(c.Telefone))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();

        var jaEnviados = await JaEnviadosHoje(empresaId, TipoDisparoWhatsApp.Aniversario);

        int enviados = 0, falhas = 0;
        foreach (var c in destinatarios.Where(c => !jaEnviados.Contains(c.Id)))
        {
            var ctx = VariaveisComuns(c, nomeEmpresa);
            var (ok, wamId, erro) = await Enviar(empresaId, c, TipoDisparoWhatsApp.Aniversario, template, ctx, cfg);
            if (ok) enviados++; else falhas++;
        }

        logger.LogInformation("[WhatsApp] Aniversariantes {Empresa}: {E} enviados, {F} falhas",
            nomeEmpresa, enviados, falhas);
    }

    // ─── Promoções ───────────────────────────────────────────────────────────

    /// <summary>
    /// Envia promoções de produtos com validade próxima.
    /// Usa as ArteMarketing criadas hoje pelo ValidadeJob (tipo "Feed", com dados de promoção).
    /// Envia uma mensagem por produto em promoção para TODOS os clientes ativos com telefone.
    /// Para evitar spam: no máximo 1 disparo de promoção por dia para o mesmo cliente.
    /// </summary>
    private async Task DispararPromocoes(Guid empresaId, string nomeEmpresa,
        ConfiguracaoWhatsAppMensagem cfg)
    {
        var template = await ObterTemplate(empresaId, TipoDisparoWhatsApp.Promocao);
        if (template is null)
        {
            logger.LogWarning("[WhatsApp] {Empresa}: sem template de Promoção configurado.", nomeEmpresa);
            return;
        }

        // Busca artes de promoção geradas hoje pelo ValidadeJob
        var hoje = DateTime.Today;
        var artesHoje = await db.ArtesMarketing.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId
                     && a.CriadoEm.Date == hoje
                     && a.Formato == FormatoArte.FeedQuadrado
                     && a.LayoutJson != null)
            .ToListAsync();

        if (artesHoje.Count == 0)
        {
            logger.LogInformation("[WhatsApp] {Empresa}: nenhuma promoção gerada hoje.", nomeEmpresa);
            return;
        }

        // Clientes que ainda não receberam promoção hoje
        var jaEnviados = await JaEnviadosHoje(empresaId, TipoDisparoWhatsApp.Promocao);
        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId
                     && c.Ativo
                     && !string.IsNullOrEmpty(c.Telefone)
                     && !jaEnviados.Contains(c.Id))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();

        if (clientes.Count == 0) return;

        // Extrai dados da primeira arte (produto principal em promoção)
        var arte   = artesHoje[0];
        var layout = ParseLayout(arte.LayoutJson);

        int enviados = 0, falhas = 0;
        foreach (var c in clientes)
        {
            var ctx = VariaveisComuns(c, nomeEmpresa);
            ctx["produto_nome"]        = layout.GetValueOrDefault("produto_nome", "");
            ctx["produto_preco"]       = layout.GetValueOrDefault("produto_preco", "");
            ctx["produto_preco_promo"] = layout.GetValueOrDefault("produto_preco_promo", "");
            ctx["data_validade"]       = layout.GetValueOrDefault("data_validade", "");
            ctx["desconto"]            = layout.GetValueOrDefault("desconto", "");
            var (ok, _, _) = await Enviar(empresaId, c, TipoDisparoWhatsApp.Promocao, template, ctx, cfg);
            if (ok) enviados++; else falhas++;
        }

        logger.LogInformation("[WhatsApp] Promoções {Empresa}: {E} enviados, {F} falhas",
            nomeEmpresa, enviados, falhas);
    }

    // ─── Novidades ────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispara mensagem de novidade para todos os clientes ativos com telefone.
    /// Deve ser acionado manualmente via endpoint /api/whatsapp/mensagem/disparar-novidade
    /// OU marcado como ativo para um disparo único diário às 8h.
    /// Para evitar spam: só envia se não houve disparo de novidade há menos de 7 dias.
    /// </summary>
    private async Task DispararNovidades(Guid empresaId, string nomeEmpresa,
        ConfiguracaoWhatsAppMensagem cfg)
    {
        var template = await ObterTemplate(empresaId, TipoDisparoWhatsApp.Novidade);
        if (template is null)
        {
            logger.LogWarning("[WhatsApp] {Empresa}: sem template de Novidade configurado.", nomeEmpresa);
            return;
        }

        // Proteção anti-spam: verifica se já disparou novidade nos últimos 7 dias
        var limite = DateTime.UtcNow.AddDays(-7);
        var disparouRecente = await db.HistoricosMensagensWhatsApp.AsNoTracking()
            .AnyAsync(h => h.EmpresaId == empresaId
                        && h.TipoDisparo == TipoDisparoWhatsApp.Novidade
                        && h.EnviadoEm >= limite
                        && h.Status != StatusMensagemWhatsApp.Falhou);

        if (disparouRecente)
        {
            logger.LogInformation("[WhatsApp] {Empresa}: novidade já enviada nos últimos 7 dias. Pulando.", nomeEmpresa);
            return;
        }

        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId
                     && c.Ativo
                     && !string.IsNullOrEmpty(c.Telefone))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();

        int enviados = 0, falhas = 0;
        foreach (var c in clientes)
        {
            var ctx = VariaveisComuns(c, nomeEmpresa);
            var (ok, _, _) = await Enviar(empresaId, c, TipoDisparoWhatsApp.Novidade, template, ctx, cfg);
            if (ok) enviados++; else falhas++;
        }

        logger.LogInformation("[WhatsApp] Novidades {Empresa}: {E} enviados, {F} falhas",
            nomeEmpresa, enviados, falhas);
    }

    /// <summary>Igual a DispararNovidades mas sem a proteção de 7 dias (disparo manual).</summary>
    private async Task DispararNovidadesManual(Guid empresaId, string nomeEmpresa,
        ConfiguracaoWhatsAppMensagem cfg)
    {
        var template = await ObterTemplate(empresaId, TipoDisparoWhatsApp.Novidade);
        if (template is null)
        {
            logger.LogWarning("[WhatsApp] {Empresa}: sem template de Novidade configurado.", nomeEmpresa);
            return;
        }

        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId
                     && c.Ativo
                     && !string.IsNullOrEmpty(c.Telefone))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();

        int enviados = 0, falhas = 0;
        foreach (var c in clientes)
        {
            var ctx = VariaveisComuns(c, nomeEmpresa);
            var (ok, _, _) = await Enviar(empresaId, c, TipoDisparoWhatsApp.Novidade, template, ctx, cfg);
            if (ok) enviados++; else falhas++;
        }

        logger.LogInformation("[WhatsApp] Novidades manual {Empresa}: {E} enviados, {F} falhas",
            nomeEmpresa, enviados, falhas);
    }

    // ─── Disparos manuais (chamados via Hangfire.BackgroundJob.Enqueue) ───────

    /// <summary>Dispara promoções manualmente para uma empresa específica.</summary>
    public async Task DispararPromocaoManualAsync(Guid empresaId)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new { e.Id, e.NomeFantasia })
            .FirstOrDefaultAsync();

        if (empresa is null) return;

        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId);

        if (cfg is null || !cfg.Ativo
            || string.IsNullOrEmpty(cfg.PhoneNumberId)
            || string.IsNullOrEmpty(cfg.AccessToken))
        {
            logger.LogWarning("[WhatsApp] {Empresa}: WhatsApp não configurado para promoções manuais.", empresa.NomeFantasia);
            return;
        }

        await DispararPromocoes(empresa.Id, empresa.NomeFantasia, cfg);
    }

    /// <summary>Dispara novidades manualmente para uma empresa específica (ignora proteção de 7 dias).</summary>
    public async Task DispararNovidadeManualAsync(Guid empresaId)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new { e.Id, e.NomeFantasia })
            .FirstOrDefaultAsync();

        if (empresa is null) return;

        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId);

        if (cfg is null || !cfg.Ativo
            || string.IsNullOrEmpty(cfg.PhoneNumberId)
            || string.IsNullOrEmpty(cfg.AccessToken))
        {
            logger.LogWarning("[WhatsApp] {Empresa}: WhatsApp não configurado para novidades manuais.", empresa.NomeFantasia);
            return;
        }

        await DispararNovidadesManual(empresa.Id, empresa.NomeFantasia, cfg);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<TemplateWhatsAppMensagem?> ObterTemplate(Guid empresaId, TipoDisparoWhatsApp tipo)
        => await db.TemplatesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmpresaId == empresaId && t.TipoDisparo == tipo && t.Ativo);

    private async Task<List<Guid?>> JaEnviadosHoje(Guid empresaId, TipoDisparoWhatsApp tipo)
    {
        var hoje = DateTime.Today;
        return await db.HistoricosMensagensWhatsApp.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId
                     && h.TipoDisparo == tipo
                     && h.EnviadoEm.Date == hoje
                     && h.Status != StatusMensagemWhatsApp.Falhou)
            .Select(h => h.ClienteId)
            .ToListAsync();
    }

    private async Task<(bool ok, string? wamId, string? erro)> Enviar(
        Guid empresaId, ClienteInfo cliente, TipoDisparoWhatsApp tipo,
        TemplateWhatsAppMensagem template, Dictionary<string, string> contexto,
        ConfiguracaoWhatsAppMensagem cfg)
    {
        var variaveis = ResolverVariaveis(template.VariaveisJson, contexto);

        var historico = HistoricoMensagemWhatsApp.Criar(
            empresaId, cliente.Id, cliente.Telefone, cliente.Nome, tipo, template.NomeMeta);

        db.HistoricosMensagensWhatsApp.Add(historico);
        await db.SaveChangesAsync();

        var (sucesso, wamId, erro) = await whatsApp.EnviarTemplate(
            cfg.PhoneNumberId!, cfg.AccessToken!,
            cliente.Telefone, template.NomeMeta, template.Idioma, variaveis);

        if (sucesso) historico.MarcarEnviada(wamId!);
        else         historico.MarcarFalha(erro ?? "Erro desconhecido");

        await db.SaveChangesAsync();
        return (sucesso, wamId, erro);
    }

    private static IEnumerable<string> ResolverVariaveis(string? variaveisJson, Dictionary<string, string> ctx)
    {
        if (string.IsNullOrEmpty(variaveisJson)) return [];
        try
        {
            var mapeamentos = JsonSerializer.Deserialize<List<VariavelMap>>(variaveisJson) ?? [];
            return mapeamentos
                .OrderBy(v => v.Posicao)
                .Select(v => ctx.TryGetValue(v.Campo, out var val) ? val : "")
                .ToList();
        }
        catch { return []; }
    }

    private static Dictionary<string, string> ParseLayout(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch { return []; }
    }
}

internal record ClienteInfo(Guid Id, string Nome, string Telefone, DateTime? DataNascimento = null);
internal record VariavelMap(int Posicao, string Campo);
