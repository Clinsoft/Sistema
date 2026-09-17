using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Preço observado de um item num concorrente (coleta manual em visita). Quando ligado
/// a um Produto nosso (ProdutoId), entra no comparativo nosso × concorrente.
/// </summary>
public class PrecoConcorrente : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ConcorrenteId { get; private set; }
    public Guid? ProdutoId { get; private set; }   // nosso produto (p/ comparação); nulo = item livre
    public string Descricao { get; private set; } = null!;
    public string? Ean { get; private set; }
    public decimal Preco { get; private set; }
    public string Unidade { get; private set; } = "un";   // kg, un, 100g, L...
    public DateTime DataColeta { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public string? Observacao { get; private set; }

    private PrecoConcorrente() { }

    public static PrecoConcorrente Criar(Guid empresaId, Guid concorrenteId, string descricao,
        decimal preco, string unidade, Guid? produtoId = null, string? ean = null,
        Guid? usuarioId = null, string? observacao = null)
        => new()
        {
            EmpresaId = empresaId, ConcorrenteId = concorrenteId, ProdutoId = produtoId,
            Descricao = descricao, Ean = ean, Preco = preco,
            Unidade = string.IsNullOrWhiteSpace(unidade) ? "un" : unidade.Trim(),
            DataColeta = DateTime.Now, UsuarioId = usuarioId, Observacao = observacao,
        };

    public void Editar(string descricao, decimal preco, string unidade, Guid? produtoId,
        string? ean, string? observacao)
    {
        Descricao = descricao; Preco = preco;
        Unidade = string.IsNullOrWhiteSpace(unidade) ? "un" : unidade.Trim();
        ProdutoId = produtoId; Ean = ean; Observacao = observacao;
    }
}
