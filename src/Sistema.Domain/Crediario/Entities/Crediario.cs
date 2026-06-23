using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Crediario.Entities;

public class Crediario : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid? VendaId { get; private set; }
    public string Numero { get; private set; } = null!;
    public decimal ValorTotal { get; private set; }
    public decimal ValorEntrada { get; private set; }
    public decimal ValorFinanciado { get; private set; }
    public int NumeroParcelas { get; private set; }
    public decimal TaxaJurosMensal { get; private set; }
    public DateTime DataContrato { get; private set; }
    public StatusCrediario Status { get; private set; }
    public Guid UsuarioId { get; private set; }

    private readonly List<ParcelaCrediario> _parcelas = [];
    public IReadOnlyList<ParcelaCrediario> Parcelas => _parcelas.AsReadOnly();

    private Crediario() { }

    public static Crediario Criar(Guid empresaId, Guid clienteId, Guid usuarioId,
        string numero, decimal valorTotal, decimal valorEntrada,
        int numeroParcelas, decimal taxaJurosMensal, DateTime dataContrato,
        Guid? vendaId = null)
    {
        var valorFinanciado = valorTotal - valorEntrada;
        var crediario = new Crediario
        {
            EmpresaId = empresaId,
            ClienteId = clienteId,
            UsuarioId = usuarioId,
            VendaId = vendaId,
            Numero = numero,
            ValorTotal = valorTotal,
            ValorEntrada = valorEntrada,
            ValorFinanciado = valorFinanciado,
            NumeroParcelas = numeroParcelas,
            TaxaJurosMensal = taxaJurosMensal,
            DataContrato = dataContrato,
            Status = StatusCrediario.Ativo
        };

        crediario.GerarParcelas();
        return crediario;
    }

    private void GerarParcelas()
    {
        // Tabela Price (amortização francesa)
        var pmt = CalcularPmt(ValorFinanciado, TaxaJurosMensal / 100, NumeroParcelas);

        for (int i = 1; i <= NumeroParcelas; i++)
        {
            var vencimento = DataContrato.AddMonths(i);
            _parcelas.Add(ParcelaCrediario.Criar(Id, i, Math.Round(pmt, 2), vencimento));
        }
    }

    private static decimal CalcularPmt(decimal pv, decimal taxa, int n)
    {
        if (taxa == 0) return pv / n;
        var fator = (double)taxa;
        var parcela = (double)pv * fator * Math.Pow(1 + fator, n) / (Math.Pow(1 + fator, n) - 1);
        return (decimal)parcela;
    }

    public decimal SaldoDevedor() => _parcelas
        .Where(p => p.Status != StatusParcela.Paga)
        .Sum(p => p.ValorAtualizado());

    public bool Inadimplente() => _parcelas.Any(p =>
        p.Status == StatusParcela.EmAberto && p.DataVencimento.Date < DateTime.Today);

    public void Liquidar() => Status = StatusCrediario.Liquidado;
    public void Cancelar() => Status = StatusCrediario.Cancelado;
}

public enum StatusCrediario { Ativo, Liquidado, Cancelado, Renegociado }
