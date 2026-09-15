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
        // Itera as CONFIGS de mensagem ativas com credenciais (uma por loja/matriz).
        var configs = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .Where(c => c.Ativo && c.PhoneNumberId != null && c.AccessToken != null)
            .ToListAsync();

        // Nome fantasia por empresa (para logs).
        var empresaIds = configs.Select(c => c.EmpresaId).Distinct().ToList();
        var nomes = await db.Empresas.AsNoTracking()
            .Where(e => empresaIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.NomeFantasia);

        // Os clientes NÃO têm loja — são da empresa inteira. Para não enviar 2x ao mesmo
        // cliente quando há mais de uma loja/config ativa na mesma empresa, só a config
        // "matriz" dispara as campanhas (aniversário/promoção/novidade): a que tiver
        // LocalEstoqueId == null; se todas tiverem loja, a primeira por CriadoEm.
        var matrizPorEmpresa = configs
            .GroupBy(c => c.EmpresaId)
            .ToDictionary(
                g => g.Key,
                g => (g.FirstOrDefault(c => c.LocalEstoqueId == null)
                      ?? g.OrderBy(c => c.CriadoEm).First()).Id);

        foreach (var cfg in configs)
        {
            // Cada loja dispara para os SEUS clientes (Cliente.LocalEstoqueId), do seu
            // próprio número. A config "matriz" (a sem loja, ou a mais antiga) também
            // cobre os clientes SEM loja definida — assim ninguém fica sem receber e
            // não há envio duplicado.
            var ehMatriz = matrizPorEmpresa[cfg.EmpresaId] == cfg.Id;

            var nomeEmpresa = nomes.TryGetValue(cfg.EmpresaId, out var n) ? n : "";
            var localEstoqueId = cfg.LocalEstoqueId;

            if (cfg.EnviarAniversario)
                await DispararAniversariantes(cfg.EmpresaId, nomeEmpresa, cfg, localEstoqueId, ehMatriz);

            if (cfg.EnviarPromocoes)
                await DispararPromocoes(cfg.EmpresaId, nomeEmpresa, cfg, localEstoqueId, ehMatriz);

            if (cfg.EnviarNovidades)
                await DispararNovidades(cfg.EmpresaId, nomeEmpresa, cfg, localEstoqueId, ehMatriz);
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
        ConfiguracaoWhatsAppMensagem cfg, Guid? localEstoqueId = null, bool ehMatriz = true)
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
                     && !string.IsNullOrEmpty(c.Telefone)
                     && (localEstoqueId == null
                         || c.LocalEstoqueId == localEstoqueId
                         || (ehMatriz && c.LocalEstoqueId == null)))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();

        var jaEnviados = await JaEnviadosHoje(empresaId, TipoDisparoWhatsApp.Aniversario);
        var colaboradores = await TelefonesColaboradoresAsync(empresaId);

        int enviados = 0, falhas = 0;
        foreach (var c in destinatarios.Where(c => !jaEnviados.Contains(c.Id) && !EhColaborador(colaboradores, c.Telefone)))
        {
            var ctx = VariaveisComuns(c, nomeEmpresa);
            var (ok, wamId, erro) = await Enviar(empresaId, c, TipoDisparoWhatsApp.Aniversario, template, ctx, cfg, localEstoqueId);
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
        ConfiguracaoWhatsAppMensagem cfg, Guid? localEstoqueId = null, bool ehMatriz = true)
    {
        var template = await ObterTemplate(empresaId, TipoDisparoWhatsApp.Promocao);
        if (template is null)
        {
            logger.LogWarning("[WhatsApp] {Empresa}: sem template de Promoção configurado.", nomeEmpresa);
            return;
        }

        // Busca a promoção ativa vigente (módulo Promoções) — fonte única de verdade.
        var hoje = DateTime.Today;
        var promo = await db.Promocoes.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativa
                     && p.DataInicio <= hoje
                     && (p.DataFim == null || p.DataFim >= hoje))
            .OrderByDescending(p => p.CriadoEm)
            .FirstOrDefaultAsync();

        if (promo is null)
        {
            logger.LogInformation("[WhatsApp] {Empresa}: nenhuma promoção ativa hoje.", nomeEmpresa);
            return;
        }

        // Produto e preços da promoção (quando aplica a um produto específico)
        var ptBR = new System.Globalization.CultureInfo("pt-BR");
        string produtoNome = "", precoDeTxt = "", precoPromoTxt = "";
        if (promo.AplicaEm == "Produto" && promo.ReferenciaId is { } pid)
        {
            var prod = await db.Produtos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
            if (prod is not null)
            {
                produtoNome = prod.Descricao;
                var precoDe = prod.PrecoVenda;
                var precoPromo = promo.TipoDesconto == "Percentual"
                    ? Math.Round(precoDe * (1 - promo.Desconto / 100m), 2)
                    : precoDe - promo.Desconto;
                precoDeTxt    = $"R$ {precoDe.ToString("0.00", ptBR)}";
                precoPromoTxt = $"R$ {precoPromo.ToString("0.00", ptBR)}";
            }
        }
        var descontoTxt = promo.TipoDesconto == "Percentual"
            ? $"{promo.Desconto:0}% de desconto"
            : $"R$ {promo.Desconto.ToString("0.00", ptBR)} de desconto";

        // Clientes que ainda não receberam promoção hoje
        var jaEnviados = await JaEnviadosHoje(empresaId, TipoDisparoWhatsApp.Promocao);
        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId
                     && c.Ativo
                     && !string.IsNullOrEmpty(c.Telefone)
                     && !jaEnviados.Contains(c.Id)
                     && (localEstoqueId == null
                         || c.LocalEstoqueId == localEstoqueId
                         || (ehMatriz && c.LocalEstoqueId == null)))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();

        var colaboradores = await TelefonesColaboradoresAsync(empresaId);
        clientes = clientes.Where(c => !EhColaborador(colaboradores, c.Telefone)).ToList();
        if (clientes.Count == 0) return;

        int enviados = 0, falhas = 0;
        foreach (var c in clientes)
        {
            var ctx = VariaveisComuns(c, nomeEmpresa);
            ctx["produto_nome"]        = produtoNome;
            ctx["produto_preco"]       = precoDeTxt;
            ctx["produto_preco_promo"] = precoPromoTxt;
            ctx["data_validade"]       = promo.DataFim?.ToString("dd/MM/yyyy") ?? "";
            ctx["desconto"]            = descontoTxt;
            ctx["nome_promocao"]       = promo.Nome;
            var (ok, _, _) = await Enviar(empresaId, c, TipoDisparoWhatsApp.Promocao, template, ctx, cfg, localEstoqueId);
            if (ok) enviados++; else falhas++;
        }

        if (ehMatriz)
            await EnviarCopiasAdminAsync(empresaId, nomeEmpresa, cfg, TipoDisparoWhatsApp.Promocao, template,
                new Dictionary<string, string>
                {
                    ["produto_nome"] = produtoNome, ["produto_preco"] = precoDeTxt,
                    ["produto_preco_promo"] = precoPromoTxt, ["data_validade"] = promo.DataFim?.ToString("dd/MM/yyyy") ?? "",
                    ["desconto"] = descontoTxt, ["nome_promocao"] = promo.Nome,
                }, localEstoqueId);

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
        ConfiguracaoWhatsAppMensagem cfg, Guid? localEstoqueId = null, bool ehMatriz = true)
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
                     && !string.IsNullOrEmpty(c.Telefone)
                     && (localEstoqueId == null
                         || c.LocalEstoqueId == localEstoqueId
                         || (ehMatriz && c.LocalEstoqueId == null)))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();

        var colaboradores = await TelefonesColaboradoresAsync(empresaId);
        clientes = clientes.Where(c => !EhColaborador(colaboradores, c.Telefone)).ToList();

        int enviados = 0, falhas = 0;
        foreach (var c in clientes)
        {
            var ctx = VariaveisComuns(c, nomeEmpresa);
            var (ok, _, _) = await Enviar(empresaId, c, TipoDisparoWhatsApp.Novidade, template, ctx, cfg, localEstoqueId);
            if (ok) enviados++; else falhas++;
        }

        if (ehMatriz)
            await EnviarCopiasAdminAsync(empresaId, nomeEmpresa, cfg, TipoDisparoWhatsApp.Novidade, template, null, localEstoqueId);

        logger.LogInformation("[WhatsApp] Novidades {Empresa}: {E} enviados, {F} falhas",
            nomeEmpresa, enviados, falhas);
    }

    /// <summary>Igual a DispararNovidades mas sem a proteção de 7 dias (disparo manual).</summary>
    private async Task DispararNovidadesManual(Guid empresaId, string nomeEmpresa,
        ConfiguracaoWhatsAppMensagem cfg, Guid? localEstoqueId = null, bool ehMatriz = true)
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
                     && !string.IsNullOrEmpty(c.Telefone)
                     && (localEstoqueId == null
                         || c.LocalEstoqueId == localEstoqueId
                         || (ehMatriz && c.LocalEstoqueId == null)))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();

        var colaboradores = await TelefonesColaboradoresAsync(empresaId);
        clientes = clientes.Where(c => !EhColaborador(colaboradores, c.Telefone)).ToList();

        int enviados = 0, falhas = 0;
        foreach (var c in clientes)
        {
            var ctx = VariaveisComuns(c, nomeEmpresa);
            var (ok, _, _) = await Enviar(empresaId, c, TipoDisparoWhatsApp.Novidade, template, ctx, cfg, localEstoqueId);
            if (ok) enviados++; else falhas++;
        }

        if (ehMatriz)
            await EnviarCopiasAdminAsync(empresaId, nomeEmpresa, cfg, TipoDisparoWhatsApp.Novidade, template, null, localEstoqueId);

        logger.LogInformation("[WhatsApp] Novidades manual {Empresa}: {E} enviados, {F} falhas",
            nomeEmpresa, enviados, falhas);
    }

    // ─── Disparos manuais (chamados via Hangfire.BackgroundJob.Enqueue) ───────

    /// <summary>Dispara promoções manualmente para uma empresa específica.</summary>
    public async Task DispararPromocaoManualAsync(Guid empresaId, Guid? localEstoqueId = null)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new { e.Id, e.NomeFantasia })
            .FirstOrDefaultAsync();

        if (empresa is null) return;

        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.LocalEstoqueId == localEstoqueId);

        if (cfg is null || !cfg.Ativo
            || string.IsNullOrEmpty(cfg.PhoneNumberId)
            || string.IsNullOrEmpty(cfg.AccessToken))
        {
            logger.LogWarning("[WhatsApp] {Empresa}: WhatsApp não configurado para promoções manuais.", empresa.NomeFantasia);
            return;
        }

        await DispararPromocoes(empresa.Id, empresa.NomeFantasia, cfg, localEstoqueId);
    }

    /// <summary>Dispara novidades manualmente para uma empresa específica (ignora proteção de 7 dias).</summary>
    public async Task DispararNovidadeManualAsync(Guid empresaId, Guid? localEstoqueId = null)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new { e.Id, e.NomeFantasia })
            .FirstOrDefaultAsync();

        if (empresa is null) return;

        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.LocalEstoqueId == localEstoqueId);

        if (cfg is null || !cfg.Ativo
            || string.IsNullOrEmpty(cfg.PhoneNumberId)
            || string.IsNullOrEmpty(cfg.AccessToken))
        {
            logger.LogWarning("[WhatsApp] {Empresa}: WhatsApp não configurado para novidades manuais.", empresa.NomeFantasia);
            return;
        }

        await DispararNovidadesManual(empresa.Id, empresa.NomeFantasia, cfg, localEstoqueId);
    }

    /// <summary>
    /// Dispara um TEMPLATE específico (já aprovado e cadastrado) para todos os clientes ativos
    /// da loja informada, pelo número dela. Colaboradores são excluídos. Usado para "enviar uma
    /// campanha escolhendo o template", em cada loja com seu próprio número.
    /// </summary>
    public async Task DispararTemplateManualAsync(Guid empresaId, string nomeMeta, Guid? localEstoqueId = null, bool incluirSemLoja = false)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId).Select(e => new { e.Id, e.NomeFantasia }).FirstOrDefaultAsync();
        if (empresa is null) return;

        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.LocalEstoqueId == localEstoqueId);
        if (cfg is null || !cfg.Ativo || string.IsNullOrEmpty(cfg.PhoneNumberId) || string.IsNullOrEmpty(cfg.AccessToken))
        {
            logger.LogWarning("[WhatsApp] {Empresa}: WhatsApp não configurado para o disparo de template.", empresa.NomeFantasia);
            return;
        }

        var template = await db.TemplatesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmpresaId == empresaId && t.NomeMeta == nomeMeta);
        if (template is null)
        {
            logger.LogWarning("[WhatsApp] {Empresa}: template {Nome} não cadastrado no sistema.", empresa.NomeFantasia, nomeMeta);
            return;
        }

        var colaboradores = await TelefonesColaboradoresAsync(empresaId);
        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo && !string.IsNullOrEmpty(c.Telefone)
                     && (c.LocalEstoqueId == localEstoqueId || (incluirSemLoja && c.LocalEstoqueId == null)))
            .Select(c => new ClienteInfo(c.Id, c.Nome, c.Telefone!, c.DataNascimento))
            .ToListAsync();
        clientes = clientes.Where(c => !EhColaborador(colaboradores, c.Telefone)).ToList();

        int enviados = 0, falhas = 0;
        foreach (var c in clientes)
        {
            var ctx = VariaveisComuns(c, empresa.NomeFantasia);
            var (ok, _, _) = await Enviar(empresaId, c, template.TipoDisparo, template, ctx, cfg, localEstoqueId);
            if (ok) enviados++; else falhas++;
        }

        // Cópia para os administradores (uma vez, na passagem da 1ª loja).
        if (incluirSemLoja)
            await EnviarCopiasAdminAsync(empresaId, empresa.NomeFantasia, cfg, template.TipoDisparo, template, null, localEstoqueId);

        logger.LogInformation("[WhatsApp] Template {Nome} {Empresa}: {E} enviados, {F} falhas",
            nomeMeta, empresa.NomeFantasia, enviados, falhas);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<TemplateWhatsAppMensagem?> ObterTemplate(Guid empresaId, TipoDisparoWhatsApp tipo)
        => await db.TemplatesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmpresaId == empresaId && t.TipoDisparo == tipo && t.Ativo);

    // Telefones dos COLABORADORES (tabela Usuarios) — nunca recebem campanha, mesmo que
    // tenham sido cadastrados como cliente (ativo ou desativado). Casa pelos últimos 8
    // dígitos para ignorar diferenças de DDI/DDD/formatação.
    private async Task<HashSet<string>> TelefonesColaboradoresAsync(Guid empresaId)
    {
        var fones = await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Telefone != null && u.Telefone != "")
            .Select(u => u.Telefone!)
            .ToListAsync();
        return fones.Select(Fim8).Where(s => s.Length >= 8).ToHashSet();
    }

    private static string SoDigitos(string? v) => new((v ?? "").Where(char.IsDigit).ToArray());
    private static string Fim8(string? v) { var d = SoDigitos(v); return d.Length <= 8 ? d : d[^8..]; }
    private static bool EhColaborador(HashSet<string> colaboradores, string? telefone)
        => colaboradores.Contains(Fim8(telefone));

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
        ConfiguracaoWhatsAppMensagem cfg, Guid? localEstoqueId = null)
    {
        var variaveis = ResolverVariaveis(template.VariaveisJson, contexto);

        var historico = HistoricoMensagemWhatsApp.Criar(
            empresaId, cliente.Id, cliente.Telefone, cliente.Nome, tipo, template.NomeMeta, localEstoqueId);

        db.HistoricosMensagensWhatsApp.Add(historico);
        await db.SaveChangesAsync();

        var (sucesso, wamId, erro) = await whatsApp.EnviarTemplate(
            cfg.PhoneNumberId!, cfg.AccessToken!,
            cliente.Telefone, template.NomeMeta, template.Idioma, variaveis,
            string.IsNullOrWhiteSpace(template.HeaderImageUrl) ? null : template.HeaderImageUrl);

        if (sucesso) historico.MarcarEnviada(wamId!);
        else         historico.MarcarFalha(erro ?? "Erro desconhecido");

        await db.SaveChangesAsync();
        return (sucesso, wamId, erro);
    }

    /// <summary>Envia uma CÓPIA do disparo para os telefones dos ADMINISTRADORES (acompanhamento).
    /// Chamado uma única vez por disparo (não por loja). Logado com cliente nulo.</summary>
    private async Task EnviarCopiasAdminAsync(Guid empresaId, string nomeEmpresa,
        ConfiguracaoWhatsAppMensagem cfg, TipoDisparoWhatsApp tipo, TemplateWhatsAppMensagem template,
        Dictionary<string, string>? extra, Guid? localEstoqueId)
    {
        var admins = await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Perfil == "Administrador"
                     && u.Telefone != null && u.Telefone != "")
            .Select(u => new { u.Nome, u.Telefone })
            .ToListAsync();

        foreach (var a in admins)
        {
            var ctx = VariaveisComuns(new ClienteInfo(Guid.Empty, a.Nome, a.Telefone!, null), nomeEmpresa);
            if (extra != null) foreach (var kv in extra) ctx[kv.Key] = kv.Value;
            var variaveis = ResolverVariaveis(template.VariaveisJson, ctx);

            var hist = HistoricoMensagemWhatsApp.Criar(
                empresaId, null, a.Telefone!, a.Nome + " (admin)", tipo, template.NomeMeta, localEstoqueId);
            db.HistoricosMensagensWhatsApp.Add(hist);
            await db.SaveChangesAsync();

            var (ok, wamId, erro) = await whatsApp.EnviarTemplate(
                cfg.PhoneNumberId!, cfg.AccessToken!, a.Telefone!, template.NomeMeta, template.Idioma, variaveis,
                string.IsNullOrWhiteSpace(template.HeaderImageUrl) ? null : template.HeaderImageUrl);
            if (ok) hist.MarcarEnviada(wamId!); else hist.MarcarFalha(erro ?? "Erro desconhecido");
            await db.SaveChangesAsync();
        }
    }

    // O VariaveisJson usa chaves minúsculas ("posicao"/"campo"); sem case-insensitive,
    // o record (Posicao/Campo) desserializava com Campo=null e o disparo saía com 0
    // variáveis → Meta 132000. Case-insensitive + guarda de null resolvem.
    private static readonly JsonSerializerOptions _jsonVarsCI = new() { PropertyNameCaseInsensitive = true };

    private static IEnumerable<string> ResolverVariaveis(string? variaveisJson, Dictionary<string, string> ctx)
    {
        if (string.IsNullOrEmpty(variaveisJson)) return [];
        try
        {
            var mapeamentos = JsonSerializer.Deserialize<List<VariavelMap>>(variaveisJson, _jsonVarsCI) ?? [];
            return mapeamentos
                .OrderBy(v => v.Posicao)
                .Select(v => v.Campo is not null && ctx.TryGetValue(v.Campo, out var val) ? val : "")
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
internal record VariavelMap(int Posicao, string? Campo);
