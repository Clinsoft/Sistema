using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;
using System.Text.Json;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Executa disparos automáticos de WhatsApp:
/// aniversariantes, promoções e novidades.
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
        }
    }

    private async Task DispararAniversariantes(Guid empresaId, string nomeEmpresa,
        ConfiguracaoWhatsAppMensagem cfg)
    {
        var template = await db.TemplatesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmpresaId == empresaId
                && t.TipoDisparo == TipoDisparoWhatsApp.Aniversario
                && t.Ativo);

        if (template is null)
        {
            logger.LogWarning("[WhatsApp] Empresa {Id}: sem template de aniversário configurado.", empresaId);
            return;
        }

        var hoje = DateTime.Today;
        var aniversariantes = await db.Clientes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId
                     && c.Ativo
                     && c.DataNascimento.HasValue
                     && c.DataNascimento.Value.Month == hoje.Month
                     && c.DataNascimento.Value.Day   == hoje.Day
                     && !string.IsNullOrEmpty(c.Telefone))
            .Select(c => new { c.Id, c.Nome, c.Telefone })
            .ToListAsync();

        // Não reenviar para quem já recebeu hoje
        var jaEnviados = await db.HistoricosMensagensWhatsApp.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId
                     && h.TipoDisparo == TipoDisparoWhatsApp.Aniversario
                     && h.EnviadoEm.Date == hoje
                     && h.Status != StatusMensagemWhatsApp.Falhou)
            .Select(h => h.ClienteId)
            .ToListAsync();

        int enviados = 0, falhas = 0;
        foreach (var cliente in aniversariantes.Where(c => !jaEnviados.Contains(c.Id)))
        {
            // Resolve variáveis do template
            var variaveis = ResolverVariaveis(template.VariaveisJson, new
            {
                nome_cliente    = cliente.Nome,
                primeiro_nome   = cliente.Nome.Split(' ')[0],
                nome_empresa    = nomeEmpresa,
            });

            var historico = HistoricoMensagemWhatsApp.Criar(
                empresaId, cliente.Id, cliente.Telefone!, cliente.Nome,
                TipoDisparoWhatsApp.Aniversario, template.NomeMeta);

            db.HistoricosMensagensWhatsApp.Add(historico);
            await db.SaveChangesAsync(); // salva antes de enviar para registrar tentativa

            var (sucesso, wamId, erro) = await whatsApp.EnviarTemplate(
                cfg.PhoneNumberId!, cfg.AccessToken!,
                cliente.Telefone!, template.NomeMeta, template.Idioma, variaveis);

            if (sucesso) { historico.MarcarEnviada(wamId!); enviados++; }
            else         { historico.MarcarFalha(erro ?? "Erro desconhecido"); falhas++; }

            await db.SaveChangesAsync();
        }

        logger.LogInformation(
            "[WhatsApp] Aniversariantes {Empresa}: {Env} enviados, {Fa} falhas",
            nomeEmpresa, enviados, falhas);
    }

    private static IEnumerable<string> ResolverVariaveis(string? variaveisJson, object contexto)
    {
        if (string.IsNullOrEmpty(variaveisJson)) return [];

        try
        {
            var mapeamentos = JsonSerializer.Deserialize<List<VariavelMap>>(variaveisJson) ?? [];
            var ctx = JsonSerializer.Deserialize<Dictionary<string, string>>(
                JsonSerializer.Serialize(contexto)) ?? [];

            return mapeamentos
                .OrderBy(v => v.Posicao)
                .Select(v => ctx.TryGetValue(v.Campo, out var val) ? val : "")
                .ToList();
        }
        catch { return []; }
    }
}

internal record VariavelMap(int Posicao, string Campo);
