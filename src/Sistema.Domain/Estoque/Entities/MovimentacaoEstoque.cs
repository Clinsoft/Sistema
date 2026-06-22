using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class MovimentacaoEstoque : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public Guid? LoteId { get; private set; }
    public TipoMovimentacao Tipo { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal CustoUnitario { get; private set; }
    public string? DocumentoOrigem { get; private set; }
    public string? Observacao { get; private set; }
    public Guid? UsuarioId { get; private set; }

    private MovimentacaoEstoque() { }

    public static MovimentacaoEstoque Criar(Guid empresaId, Guid produtoId, Guid localEstoqueId,
        TipoMovimentacao tipo, decimal quantidade, decimal custoUnitario,
        Guid? loteId = null, string? documentoOrigem = null, Guid? usuarioId = null, string? observacao = null)
        => new()
        {
            EmpresaId = empresaId,
            ProdutoId = produtoId,
            LocalEstoqueId = localEstoqueId,
            LoteId = loteId,
            Tipo = tipo,
            Quantidade = quantidade,
            CustoUnitario = custoUnitario,
            DocumentoOrigem = documentoOrigem,
            UsuarioId = usuarioId,
            Observacao = observacao
        };
}

public enum TipoMovimentacao
{
    Entrada,
    Saida,
    AjustePositivo,
    AjusteNegativo,
    Transferencia,
    Devolucao,
    Inventario
}
