using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class SugestaoProduto : Entity
{
    public Guid ProdutoId { get; private set; }
    public string Titulo { get; private set; } = null!;
    public string Descricao { get; private set; } = null!;
    public Guid? ProdutoRelacionadoId { get; private set; }    // produto sugerido em conjunto
    public TipoSugestao Tipo { get; private set; }
    public int Ordem { get; private set; }
    public bool Ativo { get; private set; } = true;

    private SugestaoProduto() { }

    public static SugestaoProduto Criar(Guid produtoId, string titulo,
        string descricao, TipoSugestao tipo, int ordem = 0,
        Guid? produtoRelacionadoId = null)
        => new()
        {
            ProdutoId = produtoId,
            Titulo = titulo,
            Descricao = descricao,
            Tipo = tipo,
            Ordem = ordem,
            ProdutoRelacionadoId = produtoRelacionadoId
        };
}

public enum TipoSugestao
{
    ModoDeUso,
    ComoConsumir,
    CombinaCom,
    Beneficio,
    Contraindicacao
}
