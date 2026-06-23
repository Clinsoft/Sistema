using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

public class ItemPedidoWhatsApp : Entity
{
    public Guid PedidoWhatsAppId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal Total { get; private set; }

    private ItemPedidoWhatsApp() { }

    public static ItemPedidoWhatsApp Criar(Guid pedidoId, Guid produtoId, string descricao,
        decimal quantidade, decimal precoUnitario)
        => new()
        {
            PedidoWhatsAppId = pedidoId,
            ProdutoId = produtoId,
            Descricao = descricao,
            Quantidade = quantidade,
            PrecoUnitario = precoUnitario,
            Total = Math.Round(quantidade * precoUnitario, 2)
        };
}
