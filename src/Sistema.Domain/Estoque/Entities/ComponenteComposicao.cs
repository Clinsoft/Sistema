using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Um componente da composição de um produto COMPOSTO: qual produto entra, quanto e em que
/// unidade (kg para granel, un para unitário). A quantidade é POR RECEITA (rendimento do
/// produto composto). Ao "produzir", os componentes são baixados do estoque e o composto creditado.
/// </summary>
public class ComponenteComposicao : Entity
{
    public Guid ProdutoCompostoId { get; private set; }
    public Guid ComponenteProdutoId { get; private set; }
    public decimal Quantidade { get; private set; }
    public string Unidade { get; private set; } = "un";  // "kg" | "un"

    private ComponenteComposicao() { }

    public static ComponenteComposicao Criar(Guid produtoCompostoId, Guid componenteProdutoId,
        decimal quantidade, string unidade)
        => new()
        {
            ProdutoCompostoId = produtoCompostoId,
            ComponenteProdutoId = componenteProdutoId,
            Quantidade = quantidade,
            Unidade = string.IsNullOrWhiteSpace(unidade) ? "un" : unidade.Trim().ToLowerInvariant(),
        };
}
