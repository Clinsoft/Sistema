using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// No fim do dia, soma a taxa das operadoras (bruto − líquido dos recebíveis do dia)
/// e lança como despesa variável — uma conta a pagar por dia, já PAGA (a operadora
/// desconta a taxa no repasse). Idempotente pelo DocumentoOrigem e faz catch-up dos
/// últimos dias.
/// </summary>
public class TaxaCartaoDespesaJob(SistemaDbContext db, ILogger<TaxaCartaoDespesaJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecutarAsync()
    {
        var hoje = DateTime.Today;
        var limite = hoje.AddDays(-45);   // catch-up dos últimos 45 dias
        var empresas = await db.Empresas.AsNoTracking().Select(e => e.Id).ToListAsync();

        foreach (var empresaId in empresas)
        {
            // Só dias JÁ FECHADOS (< hoje): o dia corrente ainda pode receber vendas,
            // e o lançamento é idempotente — se criasse hoje, ficaria parcial pra sempre.
            var porDia = await db.ReceiveisCartao.AsNoTracking()
                .Where(r => r.EmpresaId == empresaId
                         && r.Status != StatusRecebivelCartao.Cancelado
                         && r.DataTransacao >= limite && r.DataTransacao < hoje)
                .GroupBy(r => r.DataTransacao.Date)
                .Select(g => new { Dia = g.Key, Taxa = g.Sum(x => x.ValorBruto - x.ValorLiquido) })
                .Where(x => x.Taxa > 0)
                .ToListAsync();

            var criados = 0;
            foreach (var d in porDia)
            {
                var doc = $"TAXA-CARTAO-{d.Dia:yyyyMMdd}";
                var jaExiste = await db.LancamentosFinanceiros
                    .AnyAsync(l => l.EmpresaId == empresaId && l.DocumentoOrigem == doc);
                if (jaExiste) continue;

                var valor = Math.Round(d.Taxa, 2);
                var lanc = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaPagar,
                    $"Taxas de cartão {d.Dia:dd/MM/yyyy}", valor, d.Dia,
                    documentoOrigem: doc);
                lanc.DefinirClassificacao("Despesas Variáveis", null,
                    "Gerado automaticamente das taxas de cartão do dia.");
                lanc.Baixar(valor, d.Dia);   // já paga: a operadora desconta no repasse
                db.LancamentosFinanceiros.Add(lanc);
                criados++;
            }

            if (criados > 0)
            {
                await db.SaveChangesAsync();
                logger.LogInformation("[TAXA-CARTAO] Empresa {Id}: {Qtd} lançamento(s) de despesa variável criado(s).",
                    empresaId, criados);
            }
        }
    }
}
