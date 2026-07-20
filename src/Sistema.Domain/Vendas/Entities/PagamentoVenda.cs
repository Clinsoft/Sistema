using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Vendas.Entities;

public class PagamentoVenda : Entity
{
    public Guid VendaId { get; private set; }
    public FormaPagamento Forma { get; private set; }
    public decimal Valor { get; private set; }
    public int Parcelas { get; private set; }
    public string? Descricao { get; private set; }
    /// <summary>Operadora de cartão (para calcular taxa e gerar o recebível).</summary>
    public Guid? OperadoraCartaoId { get; private set; }

    private PagamentoVenda() { }

    public static PagamentoVenda Criar(Guid vendaId, FormaPagamento forma, decimal valor,
        int parcelas = 1, string? descricao = null, Guid? operadoraCartaoId = null)
        => new()
        {
            VendaId = vendaId, Forma = forma, Valor = valor,
            Parcelas = parcelas < 1 ? 1 : parcelas, Descricao = descricao,
            OperadoraCartaoId = operadoraCartaoId,
        };
}

public enum FormaPagamento
{
    Dinheiro,
    CartaoCredito,
    CartaoDebito,
    Pix,
    Boleto,
    Cheque,
    Vale,
    Crediario
}
