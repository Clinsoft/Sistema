using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Financeiro.Entities;

/// <summary>Contrato de financiamento/empréstimo. As parcelas viram LancamentosFinanceiros
/// (categoria "Financiamentos") vinculados pelo GrupoParcelamento.</summary>
public class Financiamento : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Banco { get; private set; } = null!;
    public string? Titulo { get; private set; }              // nº do contrato/título
    public string Descricao { get; private set; } = null!;
    public decimal ValorCredito { get; private set; }        // crédito real que entrou na conta (principal)
    public decimal ValorParcela { get; private set; }
    public int NumeroParcelas { get; private set; }
    public decimal TaxaEfetivaMensal { get; private set; }   // % a.m. (0,027 = 2,7%)
    public decimal? TaxaNominalMensal { get; private set; }  // taxa informada no contrato (se houver)
    public DateTime PrimeiroVencimento { get; private set; }
    public string GrupoParcelamento { get; private set; } = null!;
    public string? ContratoPdfUrl { get; private set; }
    public bool LancouEntrada { get; private set; }

    private Financiamento() { }

    public static Financiamento Criar(Guid empresaId, string banco, string? titulo, string descricao,
        decimal valorCredito, decimal valorParcela, int numeroParcelas, decimal taxaEfetivaMensal,
        DateTime primeiroVencimento, string grupoParcelamento, decimal? taxaNominalMensal = null,
        bool lancouEntrada = false)
        => new()
        {
            EmpresaId = empresaId, Banco = banco, Titulo = titulo, Descricao = descricao,
            ValorCredito = valorCredito, ValorParcela = valorParcela, NumeroParcelas = numeroParcelas,
            TaxaEfetivaMensal = taxaEfetivaMensal, TaxaNominalMensal = taxaNominalMensal,
            PrimeiroVencimento = primeiroVencimento, GrupoParcelamento = grupoParcelamento,
            LancouEntrada = lancouEntrada
        };

    public void AnexarContrato(string url) => ContratoPdfUrl = url;
}
