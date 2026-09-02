using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Envia o RESUMO DO DIA para o WhatsApp do gestor (número configurado), via
/// template aprovado "resumo_diario_gestor" (6 parâmetros curtos, sem quebra de
/// linha — regra da Meta). Roda ~20h. Só age nas configs com o resumo ligado.
/// </summary>
public class ResumoDiarioJob(
    SistemaDbContext db,
    WhatsAppCloudApiService whatsApp,
    ILogger<ResumoDiarioJob> logger)
{
    private const string TemplateNome = "resumo_diario_gestor";

    public async Task ExecutarAsync()
    {
        var configs = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .Where(c => c.Ativo && c.EnviarResumoDiario
                     && c.TelefoneResumoDiario != null
                     && c.PhoneNumberId != null && c.AccessToken != null)
            .ToListAsync();

        if (configs.Count == 0) return;

        var hoje = DateTime.Today;
        var amanha = hoje.AddDays(1);
        var ptBR = new System.Globalization.CultureInfo("pt-BR");

        foreach (var cfg in configs)
        {
            var totalVendas = await db.Vendas.AsNoTracking()
                .Where(v => v.EmpresaId == cfg.EmpresaId && v.Status == StatusVenda.Finalizada
                         && v.DataHora >= hoje && v.DataHora < amanha)
                .Select(v => v.Total).ToListAsync();

            var total = totalVendas.Sum();
            var qtd = totalVendas.Count;
            var ticket = qtd > 0 ? total / qtd : 0m;

            var aPagar = await db.LancamentosFinanceiros.AsNoTracking()
                .Where(l => l.EmpresaId == cfg.EmpresaId && l.Tipo == TipoLancamento.ContaPagar
                         && l.Status == StatusLancamento.EmAberto
                         && l.DataVencimento >= hoje && l.DataVencimento < amanha)
                .SumAsync(l => (decimal?)(l.ValorOriginal - l.ValorPago)) ?? 0m;

            var aReceber = await db.LancamentosFinanceiros.AsNoTracking()
                .Where(l => l.EmpresaId == cfg.EmpresaId && l.Tipo == TipoLancamento.ContaReceber
                         && l.Status == StatusLancamento.EmAberto
                         && l.DataVencimento >= hoje && l.DataVencimento < amanha)
                .SumAsync(l => (decimal?)(l.ValorOriginal - l.ValorPago)) ?? 0m;

            // 6 parâmetros curtos (a Meta não aceita quebra de linha em parâmetro).
            var variaveis = new[]
            {
                hoje.ToString("dd/MM/yyyy", ptBR),
                total.ToString("N2", ptBR),
                qtd.ToString(),
                ticket.ToString("N2", ptBR),
                aPagar.ToString("N2", ptBR),
                aReceber.ToString("N2", ptBR),
            };

            // Aceita vários números separados por vírgula/;/espaço
            var numeros = cfg.TelefoneResumoDiario!
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var numero in numeros)
            {
                var (ok, _, erro) = await whatsApp.EnviarTemplate(
                    cfg.PhoneNumberId!, cfg.AccessToken!, numero, TemplateNome, "pt_BR", variaveis);
                if (ok)
                    logger.LogInformation("[WhatsApp] Resumo diário enviado para {Num}", numero);
                else
                    logger.LogWarning("[WhatsApp] Falha no resumo diário para {Num}: {Erro} "
                        + "(o template '{Tpl}' precisa estar APROVADO na Meta)", numero, erro, TemplateNome);
            }
        }
    }
}
