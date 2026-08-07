namespace Sistema.Domain.Financeiro.Entities;

/// <summary>Parcela de uma tabela de amortização Price: número, juro e amortização (juro + amort = valor da parcela).</summary>
public readonly record struct ParcelaAmortizacao(int Numero, decimal Juros, decimal Amortizacao, decimal SaldoApos);

/// <summary>Cálculo de tabela Price para financiamentos.</summary>
public static class AmortizacaoPrice
{
    /// <summary>Resolve (por bisseção) a taxa efetiva mensal que faz o valor presente das n parcelas
    /// igualar o crédito recebido. Ex.: credito 106954, parcela 6118.45, n 24 → ~0,027081.</summary>
    public static decimal ResolverTaxaMensal(decimal credito, decimal parcela, int n)
    {
        double P = (double)credito, A = (double)parcela;
        double lo = 1e-6, hi = 0.10;
        for (int k = 0; k < 200; k++)
        {
            double i = (lo + hi) / 2;
            double pv = A * (1 - Math.Pow(1 + i, -n)) / i;
            if (pv > P) lo = i; else hi = i;
        }
        return (decimal)((lo + hi) / 2);
    }

    /// <summary>Monta a tabela Price completa. A última parcela fecha o saldo ao centavo.</summary>
    public static IReadOnlyList<ParcelaAmortizacao> Montar(decimal credito, decimal parcela, int n, decimal taxaMensal)
    {
        var rows = new List<ParcelaAmortizacao>(n);
        decimal saldo = Math.Round(credito, 2);
        for (int k = 1; k <= n; k++)
        {
            decimal juros, amort;
            if (k < n)
            {
                juros = Math.Round(saldo * taxaMensal, 2);
                amort = Math.Round(parcela - juros, 2);
            }
            else
            {
                amort = Math.Round(saldo, 2);     // fecha exatamente
                juros = Math.Round(parcela - amort, 2);
            }
            saldo = Math.Round(saldo - amort, 2);
            rows.Add(new ParcelaAmortizacao(k, juros, amort, saldo));
        }
        return rows;
    }
}
