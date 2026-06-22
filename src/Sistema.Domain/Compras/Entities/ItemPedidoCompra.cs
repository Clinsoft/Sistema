using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Compras.Entities;

public class ItemPedidoCompra : Entity
{
    public Guid PedidoCompraId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal Total { get; private set; }
    public decimal QuantidadeRecebida { get; private set; }

    private ItemPedidoCompra() { }

    public static ItemPedidoCompra Criar(Guid pedidoId, Guid produtoId, string descricao,
        decimal quantidade, decimal precoUnitario)
        => new()
        {
            PedidoCompraId = pedidoId,
            ProdutoId = produtoId,
            Descricao = descricao,
            Quantidade = quantidade,
            PrecoUnitario = precoUnitario,
            Total = Math.Round(quantidade * precoUnitario, 2)
        };

    public void Receber(decimal quantidade) => QuantidadeRecebida += quantidade;
}
