using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Material de consumo / uso interno (embalagens, sacolas, etiquetas, limpeza…).
/// Cadastro SEPARADO de Produto: não é vendido, não entra no PDV, no catálogo
/// nem na formação de preço — só tem controle de estoque e custo.
/// </summary>
public class MaterialConsumo : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string Descricao { get; private set; } = null!;
    public Guid UnidadeMedidaId { get; private set; }
    public Guid? FornecedorPrincipalId { get; private set; }
    /// <summary>Código do material na nota do fornecedor (de-para para entradas de NF-e).</summary>
    public string? CodigoFornecedor { get; private set; }
    public string? CodigoBarras { get; private set; }

    public decimal EstoqueAtual { get; private set; }
    public decimal EstoqueMinimo { get; private set; }

    /// <summary>Custo médio ponderado, recalculado a cada entrada.</summary>
    public decimal CustoMedio { get; private set; }
    public decimal UltimoCusto { get; private set; }
    public DateTime? DataUltimaCompra { get; private set; }

    public string? Localizacao { get; private set; }
    public string? Observacao { get; private set; }
    public bool Ativo { get; private set; } = true;

    private MaterialConsumo() { }

    public static MaterialConsumo Criar(Guid empresaId, string codigo, string descricao,
        Guid unidadeMedidaId, Guid? fornecedorPrincipalId = null, decimal estoqueMinimo = 0)
        => new()
        {
            EmpresaId = empresaId,
            Codigo = codigo,
            Descricao = descricao,
            UnidadeMedidaId = unidadeMedidaId,
            FornecedorPrincipalId = fornecedorPrincipalId,
            EstoqueMinimo = estoqueMinimo,
        };

    public void Editar(string descricao, Guid unidadeMedidaId, Guid? fornecedorPrincipalId,
        decimal estoqueMinimo, string? localizacao, string? observacao,
        string? codigoBarras, bool ativo)
    {
        Descricao = descricao;
        UnidadeMedidaId = unidadeMedidaId;
        FornecedorPrincipalId = fornecedorPrincipalId;
        EstoqueMinimo = estoqueMinimo;
        Localizacao = localizacao;
        Observacao = observacao;
        CodigoBarras = codigoBarras;
        Ativo = ativo;
    }

    /// <summary>Registra o de-para com o fornecedor (código do material na nota dele).</summary>
    public void VincularReferenciaFornecedor(Guid fornecedorId, string? codigoNoFornecedor)
    {
        FornecedorPrincipalId = fornecedorId;
        if (!string.IsNullOrWhiteSpace(codigoNoFornecedor))
            CodigoFornecedor = codigoNoFornecedor;
    }

    /// <summary>
    /// Entrada de estoque com recálculo do custo médio ponderado.
    /// Estoque negativo (ajuste manual) é tratado como zero na média, para não
    /// distorcer o custo.
    /// </summary>
    public void EntradaEstoque(decimal quantidade, decimal custoUnitario, DateTime? dataCompra = null)
    {
        if (quantidade <= 0) throw new InvalidOperationException("Quantidade da entrada deve ser maior que zero.");

        var estoqueAnterior = EstoqueAtual > 0 ? EstoqueAtual : 0m;
        var valorAnterior = estoqueAnterior * CustoMedio;
        var valorEntrada = quantidade * custoUnitario;
        var novoEstoque = estoqueAnterior + quantidade;

        CustoMedio = novoEstoque > 0
            ? Math.Round((valorAnterior + valorEntrada) / novoEstoque, 4)
            : custoUnitario;
        UltimoCusto = custoUnitario;
        DataUltimaCompra = dataCompra ?? DateTime.Today;
        EstoqueAtual += quantidade;
    }

    /// <summary>Baixa de estoque (consumo interno, produção, perda). Não altera o custo médio.</summary>
    public void SaidaEstoque(decimal quantidade)
    {
        if (quantidade <= 0) throw new InvalidOperationException("Quantidade da saída deve ser maior que zero.");
        EstoqueAtual -= quantidade;
    }

    /// <summary>Ajuste/inventário: soma a diferença (positiva ou negativa).</summary>
    public void AjustarEstoque(decimal diferenca) => EstoqueAtual += diferenca;

    public void DefinirCustoMedio(decimal custo) => CustoMedio = custo;

    public decimal ValorEmEstoque => Math.Round(EstoqueAtual * CustoMedio, 2);
    public bool AbaixoDoMinimo => EstoqueAtual <= EstoqueMinimo;

    public void Desativar() => Ativo = false;
    public void Reativar() => Ativo = true;
}
