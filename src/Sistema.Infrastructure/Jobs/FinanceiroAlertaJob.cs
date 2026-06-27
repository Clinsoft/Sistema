using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>Alerta diário de contas a pagar e receber vencidas/a vencer.</summary>
public class FinanceiroAlertaJob(SistemaDbContext db, ILogger<FinanceiroAlertaJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecutarAsync()
    {
        var hoje = DateTime.Today;
        var amanha = hoje.AddDays(1);

        var empresas = await db.Empresas.AsNoTracking().Select(e => e.Id).ToListAsync();

        foreach (var empresaId in empresas)
        {
            // Contas a pagar vencendo hoje ou amanhã
            var aPagar = await db.LancamentosFinanceiros.AsNoTracking()
                .Where(l => l.EmpresaId == empresaId
                    && l.Tipo == TipoLancamento.ContaPagar
                    && l.Status == StatusLancamento.EmAberto
                    && (l.DataVencimento.Date == hoje || l.DataVencimento.Date == amanha))
                .Select(l => new { l.Descricao, l.ValorOriginal, l.DataVencimento })
                .ToListAsync();

            // Contas a receber vencidas (atraso > 0)
            var emAtraso = await db.LancamentosFinanceiros.AsNoTracking()
                .Where(l => l.EmpresaId == empresaId
                    && l.Tipo == TipoLancamento.ContaReceber
                    && l.Status == StatusLancamento.EmAberto
                    && l.DataVencimento < hoje)
                .CountAsync();

            if (aPagar.Any())
            {
                logger.LogWarning("[FINANCEIRO] Empresa {Id}: {Qtd} conta(s) a pagar vencendo em breve. Total: R${Total:F2}",
                    empresaId, aPagar.Count, aPagar.Sum(p => p.ValorOriginal));
            }

            if (emAtraso > 0)
            {
                logger.LogWarning("[FINANCEIRO] Empresa {Id}: {Qtd} conta(s) a receber em atraso.",
                    empresaId, emAtraso);
            }
        }
    }
}
