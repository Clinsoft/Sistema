using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class Produto : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string? CodigoBarras { get; private set; }
    public string Descricao { get; private set; } = null!;
    public string? DescricaoComplementar { get; private set; }
    public Guid CategoriaId { get; private set; }
    public Guid MarcaId { get; private set; }
    public Guid UnidadeMedidaId { get; private set; }
    public Guid? FornecedorPrincipalId { get; private set; }

    // Fiscal
    public string? Ncm { get; private set; }
    public string? Cest { get; private set; }
    public string? CstIcms { get; private set; }
    public string? CsosnIcms { get; private set; }
    public string? CstPisCofins { get; private set; }
    public decimal AliquotaIcms { get; private set; }
    public decimal AliquotaPis { get; private set; }
    public decimal AliquotaCofins { get; private set; }
    public string? Cfop { get; private set; }
    public string Origem { get; private set; } = "0"; // 0=Nacional

    // Preço e custo
    public decimal CustoUnitario { get; private set; }
    public decimal PrecoVenda { get; private set; }
    public decimal? PrecoAtacado { get; private set; }
    public decimal Markup { get; private set; }
    public decimal MargemLucro { get; private set; }

    // Estoque
    public decimal EstoqueAtual { get; private set; }
    public decimal EstoqueMinimo { get; private set; }
    public decimal EstoqueMaximo { get; private set; }
    public bool ControlarLote { get; private set; }
    public bool ControlarValidade { get; private set; }
    public bool ProdutoBalanca { get; private set; }
    public int? CodigoPlu { get; private set; }

    // Flags
    public bool Ativo { get; private set; } = true;
    public bool VendidoFracionado { get; private set; }

    private Produto() { }

    public static Produto Criar(Guid empresaId, string codigo, string descricao,
        Guid categoriaId, Guid marcaId, Guid unidadeMedidaId,
        decimal custoUnitario, decimal precoVenda,
        string? codigoBarras = null)
    {
        var markup = custoUnitario > 0 ? precoVenda / custoUnitario : 0;
        var margem = precoVenda > 0 ? (precoVenda - custoUnitario) / precoVenda * 100 : 0;

        return new Produto
        {
            EmpresaId = empresaId,
            Codigo = codigo,
            Descricao = descricao,
            CategoriaId = categoriaId,
            MarcaId = marcaId,
            UnidadeMedidaId = unidadeMedidaId,
            CustoUnitario = custoUnitario,
            PrecoVenda = precoVenda,
            CodigoBarras = codigoBarras,
            Markup = Math.Round(markup, 4),
            MargemLucro = Math.Round(margem, 2)
        };
    }

    public void AtualizarPreco(decimal novoCusto, decimal novoPreco)
    {
        CustoUnitario = novoCusto;
        PrecoVenda = novoPreco;
        Markup = novoCusto > 0 ? Math.Round(novoPreco / novoCusto, 4) : 0;
        MargemLucro = novoPreco > 0 ? Math.Round((novoPreco - novoCusto) / novoPreco * 100, 2) : 0;
    }

    public void AjustarEstoque(decimal quantidade) => EstoqueAtual += quantidade;
    public void DefinirEstoqueMinimo(decimal minimo) => EstoqueMinimo = minimo;
    public bool EstoqueAbaixoDoMinimo() => EstoqueAtual <= EstoqueMinimo;
    public void Desativar() => Ativo = false;
}
