using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class ReceitaProduto : Entity
{
    public Guid ProdutoId { get; private set; }
    public string Titulo { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public string Ingredientes { get; private set; } = null!;
    public string ModoPreparo { get; private set; } = null!;
    public string? Dicas { get; private set; }
    public int? TempoPreparoMinutos { get; private set; }
    public int? Porcoes { get; private set; }
    public string? UrlFoto { get; private set; }
    public int Ordem { get; private set; }
    public bool Ativo { get; private set; } = true;

    private ReceitaProduto() { }

    public static ReceitaProduto Criar(Guid produtoId, string titulo,
        string ingredientes, string modoPreparo, int ordem = 0)
        => new()
        {
            ProdutoId = produtoId,
            Titulo = titulo,
            Ingredientes = ingredientes,
            ModoPreparo = modoPreparo,
            Ordem = ordem
        };
}
