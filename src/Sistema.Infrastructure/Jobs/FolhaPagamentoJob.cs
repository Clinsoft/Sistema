using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Previsão mensal de folha de pagamento. Todo dia 1º gera, para cada empresa:
///  • 1 conta a pagar de salário por colaborador CLT (categoria "Pessoas", 5º dia útil);
///  • 1 conta de pró-labore por sócio (categoria "Pessoas", 5º dia útil);
///  • 1 conta de FGTS 8% sobre a folha CLT (categoria "Impostos", dia 20);
///  • 1 conta de INSS (retido dos empregados pela tabela progressiva 2026 +
///    11% de contribuinte individual / pró-labore, código DARF 1099) — categoria "Impostos", dia 20.
///
/// Regime Simples Nacional Anexo I: a contribuição patronal (CPP 20%) já está
/// embutida no DAS, por isso NÃO é lançada aqui. Idempotente por competência
/// (não duplica se rodar duas vezes no mesmo mês).
/// </summary>
public class FolhaPagamentoJob(SistemaDbContext db, ILogger<FolhaPagamentoJob> logger)
{
    // Encargos
    private const decimal AliqFgts = 0.08m;          // FGTS sobre folha CLT
    private const decimal AliqInssIndividual = 0.11m; // pró-labore — contribuinte individual, DARF 1099
    private const decimal TetoInss = 8475.55m;        // teto do salário-de-contribuição (2026)

    // Tabela INSS progressiva 2026 — (limite superior da faixa, alíquota).
    // Desconto por faixa: cada alíquota incide só sobre a parcela dentro da faixa.
    private static readonly (decimal Limite, decimal Aliq)[] FaixasInss2026 =
    [
        (1621.00m, 0.075m),
        (2902.84m, 0.09m),
        (4354.27m, 0.12m),
        (8475.55m, 0.14m),
    ];

    [AutomaticRetry(Attempts = 1)]
    public async Task ExecutarAsync() => await GerarFolhaAsync(DateTime.Today);

    /// <summary>Gera (uma única vez) as contas da folha para a competência do mês informado.</summary>
    public async Task<int> GerarFolhaAsync(DateTime referencia)
    {
        var competencia = new DateTime(referencia.Year, referencia.Month, 1);
        var docOrigem = $"FOLHA {competencia:yyyy-MM}";
        var vencSalario = QuintoDiaUtil(competencia.Year, competencia.Month);
        var vencImpostos = new DateTime(competencia.Year, competencia.Month, 20);

        var empresas = await db.Empresas.AsNoTracking().Select(e => e.Id).ToListAsync();
        var totalContas = 0;

        foreach (var empresaId in empresas)
        {
            var colaboradores = await db.Usuarios.AsNoTracking()
                .Where(u => u.EmpresaId == empresaId && u.Ativo
                    && u.Salario != null && u.Salario > 0)
                .Select(u => new { u.Id, u.Nome, u.Cargo, Salario = u.Salario!.Value })
                .ToListAsync();

            if (colaboradores.Count == 0) continue;

            // Idempotência por descrição: só insere as contas que ainda não existem
            // nesta competência (permite re-rodar para acrescentar itens novos —
            // ex.: provisões — sem duplicar o que já foi gerado).
            var existentes = await db.LancamentosFinanceiros
                .Where(l => l.EmpresaId == empresaId && l.DocumentoOrigem == docOrigem)
                .Select(l => l.Descricao)
                .ToListAsync();

            var novos = new List<LancamentoFinanceiro>();
            decimal baseFgts = 0m, inssRetido = 0m, inssIndividual = 0m;

            foreach (var c in colaboradores)
            {
                var ehSocio = EhSocio(c.Cargo);
                var tipoPag = ehSocio ? "Pró-labore" : "Salário";

                // Conta de salário / pró-labore (categoria Pessoas, 5º dia útil).
                var lancPessoal = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaPagar,
                    $"{tipoPag} {competencia:MM/yyyy} — {c.Nome}", c.Salario, vencSalario,
                    documentoOrigem: docOrigem, colaboradorId: c.Id);
                lancPessoal.DefinirClassificacao("Pessoas", c.Nome, null);
                novos.Add(lancPessoal);

                if (ehSocio)
                    inssIndividual += Math.Round(Math.Min(c.Salario, TetoInss) * AliqInssIndividual,
                        2, MidpointRounding.AwayFromZero);
                else
                {
                    baseFgts += c.Salario;
                    inssRetido += InssEmpregado(c.Salario);
                }
            }

            // FGTS 8% sobre a folha CLT (categoria Impostos, dia 20).
            if (baseFgts > 0)
            {
                var fgts = Math.Round(baseFgts * AliqFgts, 2, MidpointRounding.AwayFromZero);
                var lancFgts = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaPagar,
                    $"FGTS s/ folha {competencia:MM/yyyy}", fgts, vencImpostos, documentoOrigem: docOrigem);
                lancFgts.DefinirClassificacao("Impostos", "FGTS Digital",
                    $"8% sobre folha CLT de R$ {baseFgts:N2} (previsão)");
                novos.Add(lancFgts);
            }

            // INSS: retido dos empregados (tabela 2026) + 11% contribuinte individual (DARF 1099).
            var inssTotal = inssRetido + inssIndividual;
            if (inssTotal > 0)
            {
                var lancInss = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaPagar,
                    $"INSS folha {competencia:MM/yyyy} (retido + pró-labore 1099)", inssTotal, vencImpostos,
                    documentoOrigem: docOrigem);
                lancInss.DefinirClassificacao("Impostos", "INSS/DARF",
                    $"Retido empregados R$ {inssRetido:N2} + 11% contrib. individual cód. 1099 R$ {inssIndividual:N2}");
                novos.Add(lancInss);
            }

            // Provisões mensais (1/12) de 13º e férias sobre a folha CLT — só CLT tem
            // direito (sócio/pró-labore não). São reservas do mês, não pagamento real:
            // vencem no último dia da competência.
            if (baseFgts > 0)
            {
                var ultimoDia = new DateTime(competencia.Year, competencia.Month,
                    DateTime.DaysInMonth(competencia.Year, competencia.Month));

                var prov13 = Math.Round(baseFgts / 12m, 2, MidpointRounding.AwayFromZero);
                var provFerias = Math.Round(baseFgts * 4m / 3m / 12m, 2, MidpointRounding.AwayFromZero);

                var lanc13 = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaPagar,
                    $"Provisão 13º salário {competencia:MM/yyyy}", prov13, ultimoDia, documentoOrigem: docOrigem);
                lanc13.DefinirClassificacao("Pessoas", "Provisão",
                    $"1/12 da folha CLT de R$ {baseFgts:N2} (reserva de 13º)");
                novos.Add(lanc13);

                var lancFerias = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaPagar,
                    $"Provisão férias + 1/3 {competencia:MM/yyyy}", provFerias, ultimoDia, documentoOrigem: docOrigem);
                lancFerias.DefinirClassificacao("Pessoas", "Provisão",
                    $"(salário + 1/3)/12 da folha CLT de R$ {baseFgts:N2} (reserva de férias)");
                novos.Add(lancFerias);

                // FGTS 8% sobre as provisões de 13º e férias (categoria Impostos, dia 20).
                var fgtsProv = Math.Round((prov13 + provFerias) * AliqFgts, 2, MidpointRounding.AwayFromZero);
                var lancFgtsProv = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaPagar,
                    $"FGTS s/ provisão 13º+férias {competencia:MM/yyyy}", fgtsProv, vencImpostos,
                    documentoOrigem: docOrigem);
                lancFgtsProv.DefinirClassificacao("Impostos", "FGTS Digital",
                    $"8% sobre provisões de 13º (R$ {prov13:N2}) + férias (R$ {provFerias:N2})");
                novos.Add(lancFgtsProv);
            }

            // Filtra o que já existe (idempotência por descrição) e insere só o novo.
            var aInserir = novos.Where(n => !existentes.Contains(n.Descricao)).ToList();
            if (aInserir.Count == 0) continue;

            db.LancamentosFinanceiros.AddRange(aInserir);
            await db.SaveChangesAsync();
            totalContas += aInserir.Count;

            logger.LogInformation("[FOLHA] Empresa {Id}: {Qtd} conta(s) inserida(s) p/ competência {Comp}. " +
                "FGTS base R${Fgts:F2}, INSS retido R${Ret:F2}, INSS 1099 R${Ind:F2}.",
                empresaId, aInserir.Count, competencia.ToString("yyyy-MM"), baseFgts, inssRetido, inssIndividual);
        }

        return totalContas;
    }

    /// <summary>Sócio/proprietário → pró-labore (INSS 11% cód. 1099, sem FGTS).</summary>
    private static bool EhSocio(string? cargo)
    {
        var c = (cargo ?? string.Empty).ToLowerInvariant();
        return c.Contains("sócio") || c.Contains("socio")
            || c.Contains("proprietár") || c.Contains("proprietar")
            || c.Contains("pró-labore") || c.Contains("pro-labore");
    }

    /// <summary>INSS retido do empregado pela tabela progressiva 2026 (desconto por faixa).</summary>
    private static decimal InssEmpregado(decimal salario)
    {
        var baseCalc = Math.Min(salario, TetoInss);
        decimal anterior = 0m, total = 0m;
        foreach (var (limite, aliq) in FaixasInss2026)
        {
            if (baseCalc <= anterior) break;
            var trecho = Math.Min(baseCalc, limite) - anterior;
            if (trecho > 0) total += trecho * aliq;
            anterior = limite;
        }
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>5º dia útil do mês (considera apenas sábados/domingos — feriados não cadastrados).</summary>
    private static DateTime QuintoDiaUtil(int ano, int mes)
    {
        var d = new DateTime(ano, mes, 1);
        var uteis = 0;
        while (true)
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            {
                uteis++;
                if (uteis == 5) return d;
            }
            d = d.AddDays(1);
        }
    }
}
