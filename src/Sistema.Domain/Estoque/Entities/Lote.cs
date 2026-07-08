using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class Lote : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public string NumeroLote { get; private set; } = null!;
    public DateTime? DataFabricacao { get; private set; }
    public DateTime? DataValidade { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal CustoUnitario { get; private set; }

    private Lote() { }

    public static Lote Criar(Guid empresaId, Guid produtoId, Guid localEstoqueId,
        string numeroLote, decimal quantidade, decimal custoUnitario,
        DateTime? dataFabricacao = null, DateTime? dataValidade = null)
        => new()
        {
            EmpresaId = empresaId,
            ProdutoId = produtoId,
            LocalEstoqueId = localEstoqueId,
            NumeroLote = numeroLote,
            Quantidade = quantidade,
            CustoUnitario = custoUnitario,
            DataFabricacao = dataFabricacao,
            DataValidade = dataValidade
        };

    public bool EstaVencido() => DataValidade.HasValue && DataValidade.Value.Date < DateTime.Today;
    public bool VenceEm(int dias) => DataValidade.HasValue && DataValidade.Value.Date <= DateTime.Today.AddDays(dias);
    public void Baixar(decimal quantidade) => Quantidade = Math.Max(0, Quantidade - quantidade);
    public void AtualizarQuantidade(decimal quantidade) => Quantidade = Math.Max(0, quantidade);
    public void AtualizarValidade(DateTime dataValidade) => DataValidade = dataValidade;

    public void Editar(string numeroLote, decimal quantidade, decimal custoUnitario,
        DateTime? dataFabricacao, DateTime? dataValidade)
    {
        NumeroLote = numeroLote;
        Quantidade = Math.Max(0, quantidade);
        CustoUnitario = custoUnitario;
        DataFabricacao = dataFabricacao;
        DataValidade = dataValidade;
    }
}
