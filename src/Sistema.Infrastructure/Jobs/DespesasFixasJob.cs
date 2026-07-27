using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Mensalidades fixas de fornecedores/prestadores (ex.: honorários do contador,
/// aluguel). Todo dia 1º gera, para cada fornecedor ativo com mensalidade
/// configurada, uma conta a pagar da competência — vencimento no dia informado,
/// vinculada ao fornecedor. Idempotente por competência + fornecedor.
/// </summary>
public class DespesasFixasJob(SistemaDbContext db, ILogger<DespesasFixasJob> logger)
{
    [AutomaticRetry(Attempts = 1)]
    public async Task ExecutarAsync() => await GerarAsync(DateTime.Today);

    /// <summary>Gera (uma vez) as contas de mensalidade para a competência do mês informado.</summary>
    public async Task<int> GerarAsync(DateTime referencia)
    {
        var competencia = new DateTime(referencia.Year, referencia.Month, 1);
        var docOrigem = $"MENSALIDADE {competencia:yyyy-MM}";
        var ultimoDia = DateTime.DaysInMonth(competencia.Year, competencia.Month);
        var total = 0;

        var fornecedores = await db.Fornecedores.AsNoTracking()
            .Where(f => f.Ativo && f.MensalidadeValor != null && f.MensalidadeValor > 0
                && f.MensalidadeDiaVencimento != null)
            .Select(f => new { f.Id, f.EmpresaId, f.RazaoSocial,
                Valor = f.MensalidadeValor!.Value, Dia = f.MensalidadeDiaVencimento!.Value,
                f.MensalidadeCategoria })
            .ToListAsync();

        foreach (var f in fornecedores)
        {
            // Idempotência: não duplica a mensalidade do fornecedor nesta competência.
            var jaGerado = await db.LancamentosFinanceiros.AnyAsync(l =>
                l.EmpresaId == f.EmpresaId && l.FornecedorId == f.Id && l.DocumentoOrigem == docOrigem);
            if (jaGerado) continue;

            var dia = Math.Min(Math.Max(f.Dia, 1), ultimoDia);   // clampa p/ meses curtos
            var vencimento = new DateTime(competencia.Year, competencia.Month, dia);
            var categoria = string.IsNullOrWhiteSpace(f.MensalidadeCategoria)
                ? "Despesas Administrativas" : f.MensalidadeCategoria;

            var lanc = LancamentoFinanceiro.Criar(f.EmpresaId, TipoLancamento.ContaPagar,
                $"{f.RazaoSocial} — mensalidade {competencia:MM/yyyy}", f.Valor, vencimento,
                fornecedorId: f.Id, documentoOrigem: docOrigem);
            lanc.DefinirClassificacao(categoria, f.RazaoSocial, "Mensalidade fixa (recorrente)");

            db.LancamentosFinanceiros.Add(lanc);
            await db.SaveChangesAsync();
            total++;
        }

        if (total > 0)
            logger.LogInformation("[DESPESAS-FIXAS] {Qtd} mensalidade(s) geradas p/ competência {Comp}.",
                total, competencia.ToString("yyyy-MM"));
        return total;
    }
}
