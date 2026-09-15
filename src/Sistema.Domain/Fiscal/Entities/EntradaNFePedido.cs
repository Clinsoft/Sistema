using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Fiscal.Entities;

/// <summary>
/// Vínculo entre uma entrada (NF) e um pedido de compra. Permite que UMA nota fiscal
/// atenda VÁRIOS pedidos (o fornecedor consolida vários pedidos nossos numa só NF).
/// A entrada mantém <see cref="EntradaNFe.PedidoCompraId"/> como pedido "primário"
/// (usado no confronto de faltantes); os demais ficam nesta tabela de vínculo.
/// </summary>
public class EntradaNFePedido : Entity
{
    public Guid EntradaNFeId { get; private set; }
    public Guid PedidoCompraId { get; private set; }

    private EntradaNFePedido() { }

    public static EntradaNFePedido Criar(Guid entradaNFeId, Guid pedidoCompraId)
        => new() { EntradaNFeId = entradaNFeId, PedidoCompraId = pedidoCompraId };
}
