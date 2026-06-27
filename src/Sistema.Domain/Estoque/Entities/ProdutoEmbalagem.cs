using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class ProdutoEmbalagem : Entity
{
    public Guid ProdutoId { get; private set; }
    public Guid UnidadeMedidaId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Multiplicador { get; private set; }
    public string? CodigoBarras { get; private set; }
    public decimal? PrecoVenda { get; private set; }
    public bool Ativo { get; private set; } = true;

    private ProdutoEmbalagem() { }

    public static ProdutoEmbalagem Criar(Guid produtoId, Guid unidadeMedidaId,
        string descricao, decimal multiplicador,
        string? codigoBarras = null, decimal? precoVenda = null)
        => new()
        {
            ProdutoId = produtoId,
            UnidadeMedidaId = unidadeMedidaId,
            Descricao = descricao,
            Multiplicador = multiplicador,
            CodigoBarras = codigoBarras,
            PrecoVenda = precoVenda,
        };

    public void Editar(Guid unidadeMedidaId, string descricao, decimal multiplicador,
        string? codigoBarras, decimal? precoVenda, bool ativo)
    {
        UnidadeMedidaId = unidadeMedidaId;
        Descricao = descricao;
        Multiplicador = multiplicador;
        CodigoBarras = codigoBarras;
        PrecoVenda = precoVenda;
        Ativo = ativo;
    }
}
