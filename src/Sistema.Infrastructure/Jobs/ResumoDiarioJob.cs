using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Envia o RESUMO DO DIA para o WhatsApp do gestor (número configurado). Roda ~21h05,
/// para acompanhar o fechamento. Separa as VENDAS por unidade (loja) numa linha só e
/// mostra o financeiro do dia (a pagar/a receber) da empresa.
/// Usa o template "resumo_lojas_gestor" (5 params); se ele ainda não estiver aprovado,
/// cai no antigo "resumo_diario_gestor" (6 params, totais da empresa).
/// </summary>
public class ResumoDiarioJob(
    SistemaDbContext db,
    WhatsAppCloudApiService whatsApp,
    ILogger<ResumoDiarioJob> logger)
{
    private const string TemplateLojas  = "resumo_lojas_gestor";
    private const string TemplateAntigo = "resumo_diario_gestor";

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
            var lojas = await db.LocaisEstoque.AsNoTracking()
                .Where(l => l.EmpresaId == cfg.EmpresaId)
                .Select(l => new { l.Id, l.Nome }).OrderBy(l => l.Nome).ToListAsync();

            var vendas = await db.Vendas.AsNoTracking()
                .Where(v => v.EmpresaId == cfg.EmpresaId && v.Status == StatusVenda.Finalizada
                         && v.DataHora >= hoje && v.DataHora < amanha)
                .Select(v => new { v.LocalEstoqueId, v.Total })
                .ToListAsync();

            var totalGeral = vendas.Sum(v => v.Total);
            var qtdGeral = vendas.Count;
            var ticket = qtdGeral > 0 ? totalGeral / qtdGeral : 0m;

            // Vendas por loja numa linha só (a Meta não aceita quebra de linha em parâmetro).
            var partes = lojas.Select(l =>
            {
                var doLoja = vendas.Where(v => v.LocalEstoqueId == l.Id).ToList();
                return $"{l.Nome}: R$ {doLoja.Sum(x => x.Total).ToString("N2", ptBR)} ({doLoja.Count} vendas)";
            }).ToList();
            // Vendas sem loja (raro) entram como "Outros".
            var semLoja = vendas.Where(v => v.LocalEstoqueId == Guid.Empty
                || !lojas.Any(l => l.Id == v.LocalEstoqueId)).ToList();
            if (semLoja.Count > 0)
                partes.Add($"Outros: R$ {semLoja.Sum(x => x.Total).ToString("N2", ptBR)} ({semLoja.Count})");
            var vendasPorLoja = partes.Count > 0 ? string.Join("  ·  ", partes) : "sem vendas hoje";

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

            // Novo template (5 params): vendas por loja em uma linha.
            var paramsLojas = new[]
            {
                hoje.ToString("dd/MM/yyyy", ptBR),
                vendasPorLoja,
                totalGeral.ToString("N2", ptBR),
                aPagar.ToString("N2", ptBR),
                aReceber.ToString("N2", ptBR),
            };
            // Antigo (6 params) como fallback.
            var paramsAntigo = new[]
            {
                hoje.ToString("dd/MM/yyyy", ptBR),
                totalGeral.ToString("N2", ptBR),
                qtdGeral.ToString(),
                ticket.ToString("N2", ptBR),
                aPagar.ToString("N2", ptBR),
                aReceber.ToString("N2", ptBR),
            };

            var numeros = cfg.TelefoneResumoDiario!
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var numero in numeros)
            {
                var (ok, _, erro) = await whatsApp.EnviarTemplate(
                    cfg.PhoneNumberId!, cfg.AccessToken!, numero, TemplateLojas, "pt_BR", paramsLojas);
                if (!ok)
                {
                    logger.LogWarning("[WhatsApp] Template '{Novo}' indisponível ({Erro}); usando '{Antigo}'.",
                        TemplateLojas, erro, TemplateAntigo);
                    (ok, _, erro) = await whatsApp.EnviarTemplate(
                        cfg.PhoneNumberId!, cfg.AccessToken!, numero, TemplateAntigo, "pt_BR", paramsAntigo);
                }
                if (ok) logger.LogInformation("[WhatsApp] Resumo diário enviado para {Num}", numero);
                else logger.LogWarning("[WhatsApp] Falha no resumo diário para {Num}: {Erro}", numero, erro);
            }
        }
    }
}
