using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Vendas.Entities;

public class ItemVenda : Entity
{
    public Guid VendaId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal PercentualDesconto { get; private set; }
    public decimal TotalDesconto { get; private set; }
    public decimal Total { get; private set; }

    private ItemVenda() { }

    public static ItemVenda Criar(Guid vendaId, Guid produtoId, string descricao,
        decimal quantidade, decimal precoUnitario, decimal percentualDesconto = 0)
    {
        var totalDesconto = precoUnitario * quantidade * percentualDesconto / 100;
        return new ItemVenda
        {
            VendaId = vendaId,
            ProdutoId = produtoId,
            Descricao = descricao,
            Quantidade = quantidade,
            PrecoUnitario = precoUnitario,
            PercentualDesconto = percentualDesconto,
            TotalDesconto = Math.Round(totalDesconto, 2),
            Total = Math.Round(precoUnitario * quantidade - totalDesconto, 2)
        };
    }
}
