using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Crediario.Entities;

public class ParcelaCrediario : Entity
{
    public Guid CrediarioId { get; private set; }
    public int Numero { get; private set; }
    public decimal Valor { get; private set; }
    public decimal ValorJuros { get; private set; }
    public decimal ValorMulta { get; private set; }
    public DateTime DataVencimento { get; private set; }
    public DateTime? DataPagamento { get; private set; }
    public decimal? ValorPago { get; private set; }
    public StatusParcela Status { get; private set; }
    public Guid? ContaReceberId { get; private set; }   // FK para ContaReceber (Fase 2)
    public string? PixCopiaCola { get; private set; }

    private ParcelaCrediario() { }

    public static ParcelaCrediario Criar(Guid crediarioId, int numero,
        decimal valor, DateTime dataVencimento)
        => new()
        {
            CrediarioId = crediarioId,
            Numero = numero,
            Valor = valor,
            DataVencimento = dataVencimento,
            Status = StatusParcela.EmAberto
        };

    public decimal ValorAtualizado()
    {
        if (Status == StatusParcela.Paga) return 0;
        var diasAtraso = (DateTime.Today - DataVencimento.Date).Days;
        if (diasAtraso <= 0) return Valor;

        var multa = Valor * 0.02m;              // 2% multa fixa
        var juros = Valor * 0.001m * diasAtraso; // 0,1% ao dia
        return Math.Round(Valor + multa + juros, 2);
    }

    public void Pagar(decimal valorPago, Guid? contaReceberId = null)
    {
        ValorPago = valorPago;
        DataPagamento = DateTime.UtcNow;
        ContaReceberId ??= contaReceberId;
        Status = StatusParcela.Paga;
    }

    /// <summary>Vincula o título de Contas a Receber criado para esta parcela.</summary>
    public void VincularContaReceber(Guid contaReceberId) => ContaReceberId = contaReceberId;

    public void DefinirPix(string copiaCola) => PixCopiaCola = copiaCola;
}

public enum StatusParcela { EmAberto, Paga, Atrasada, Renegociada, Cancelada }
