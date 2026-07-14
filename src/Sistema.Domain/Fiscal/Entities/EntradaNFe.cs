using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Fiscal.Entities;

/// <summary>
/// Escrituração de entrada de mercadoria via NF-e recebida.
/// Agrega: dados da nota, itens processados, lançamentos financeiros e movimentações de estoque.
/// </summary>
public class EntradaNFe : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid NotaFiscalRecebidaId { get; private set; }

    // Dados da nota
    public string ChaveAcesso { get; private set; } = null!;
    public string EmitenteNome { get; private set; } = null!;
    public string EmitenteCnpj { get; private set; } = null!;
    public Guid? FornecedorId { get; private set; }
    public Guid? PedidoCompraId { get; private set; }
    public DateTime DataEmissao { get; private set; }
    public DateTime DataEntrada { get; private set; }
    public string? NaturezaOperacao { get; private set; }

    // Valores
    public decimal ValorProdutos { get; private set; }
    public decimal ValorFrete { get; private set; }
    public decimal ValorFreteManual { get; private set; }  // frete informado manualmente
    public decimal ValorSeguro { get; private set; }
    public decimal ValorDesconto { get; private set; }
    public decimal ValorIpi { get; private set; }
    public decimal ValorIcmsSt { get; private set; }
    public decimal ValorTotal { get; private set; }

    // Local de estoque padrão para esta entrada
    public Guid LocalEstoqueId { get; private set; }

    // Duplicatas do XML (JSON) — mantido para recarregar faturas antes de processar
    public string? DuplicatasJson { get; private set; }

    // Estado
    public StatusEntradaNFe Status { get; private set; }
    public string? Observacao { get; private set; }
    public DateTime? DataProcessamento { get; private set; }
    public DateTime? DataEstorno { get; private set; }
    public string? MotivoEstorno { get; private set; }

    // Método de rateio do frete. Hoje sempre ValorProporcional; computado (sem setter)
    // para o EF não criar coluna. Estruturado para virar campo configurável no futuro
    // (por peso, quantidade, volume).
    public MetodoRateioFrete MetodoRateioFrete => MetodoRateioFrete.ValorProporcional;

    private readonly List<ItemEntradaNFe> _itens = [];
    public IReadOnlyList<ItemEntradaNFe> Itens => _itens.AsReadOnly();

    private EntradaNFe() { }

    /// <summary>
    /// Rateia o frete total entre os itens pelo método PROPORCIONAL AO VALOR dos
    /// produtos e recalcula o custo de cada item.
    /// FatorRateio = FreteTotal / ValorTotalProdutos; FreteItem = ValorTotalItem × FatorRateio.
    /// O somatório do frete rateado é exatamente igual ao frete total — o ajuste de
    /// arredondamento é aplicado no último item.
    /// </summary>
    public void RatearFrete()
    {
        var frete = FreteTotal;
        var totalProdutos = _itens.Sum(i => i.ValorTotalXml);

        // Sem frete ou sem base de rateio → custo sem componente de frete.
        if (frete <= 0 || totalProdutos <= 0 || _itens.Count == 0)
        {
            foreach (var item in _itens) item.CalcularCusto(0m);
            return;
        }

        decimal acumulado = 0m;
        for (int idx = 0; idx < _itens.Count; idx++)
        {
            var item = _itens[idx];
            decimal freteItem;
            if (idx == _itens.Count - 1)
            {
                // Último item recebe o resto, garantindo Σ frete = FreteTotal.
                freteItem = Math.Round(frete - acumulado, 2);
            }
            else
            {
                freteItem = Math.Round(frete * (item.ValorTotalXml / totalProdutos), 2);
                acumulado += freteItem;
            }
            item.CalcularCusto(freteItem);
        }
    }

    public static EntradaNFe Criar(
        Guid empresaId, Guid notaId, string chave, string emitenteNome, string emitenteCnpj,
        DateTime dataEmissao, Guid localEstoqueId,
        decimal valProdutos, decimal valFrete, decimal valSeguro,
        decimal valDesconto, decimal valIpi, decimal valIcmsSt, decimal valTotal,
        string? natureza = null, Guid? fornecedorId = null, Guid? pedidoCompraId = null)
        => new()
        {
            EmpresaId = empresaId,
            NotaFiscalRecebidaId = notaId,
            ChaveAcesso = chave,
            EmitenteNome = emitenteNome,
            EmitenteCnpj = emitenteCnpj,
            DataEmissao = dataEmissao,
            DataEntrada = DateTime.Today,
            LocalEstoqueId = localEstoqueId,
            ValorProdutos = valProdutos,
            ValorFrete = valFrete,
            ValorSeguro = valSeguro,
            ValorDesconto = valDesconto,
            ValorIpi = valIpi,
            ValorIcmsSt = valIcmsSt,
            ValorTotal = valTotal,
            NaturezaOperacao = natureza,
            FornecedorId = fornecedorId,
            PedidoCompraId = pedidoCompraId,
            Status = StatusEntradaNFe.EmEdicao,
        };

    public void AdicionarItem(ItemEntradaNFe item) => _itens.Add(item);

    public void DefinirFreteManual(decimal valor) => ValorFreteManual = valor;

    public void DefinirLocalEstoque(Guid localId) => LocalEstoqueId = localId;

    public void DefinirDuplicatas(string json) => DuplicatasJson = json;

    public void VincularFornecedor(Guid fornecedorId) => FornecedorId = fornecedorId;

    public void VincularPedidoCompra(Guid pedidoId) => PedidoCompraId = pedidoId;

    public void Processar()
    {
        Status = StatusEntradaNFe.Processada;
        DataProcessamento = DateTime.UtcNow;
    }

    public void Estornar(string motivo)
    {
        if (Status != StatusEntradaNFe.Processada)
            throw new InvalidOperationException("Somente entradas processadas podem ser estornadas.");
        Status = StatusEntradaNFe.Estornada;
        DataEstorno = DateTime.UtcNow;
        MotivoEstorno = motivo;
    }

    public decimal FreteTotal => ValorFrete + ValorFreteManual;
}

public class ItemEntradaNFe : Entity
{
    public Guid EntradaNFeId { get; private set; }
    public int NumeroItem { get; private set; }

    // Dados do XML
    public string CfopXml { get; private set; } = null!;
    public string CfopUtilizado { get; private set; } = null!;    // pode ser editado
    public string NcmXml { get; private set; } = null!;
    public string DescricaoXml { get; private set; } = null!;
    public string? CodigoFornecedor { get; private set; }         // código do produto no fornecedor
    public string? CodigoBarras { get; private set; }
    public decimal QuantidadeXml { get; private set; }
    public string UnidadeXml { get; private set; } = null!;
    public decimal ValorUnitarioXml { get; private set; }
    public decimal ValorTotalXml { get; private set; }
    public decimal ValorIpi { get; private set; }
    public decimal ValorIcmsSt { get; private set; }
    public decimal ValorFreteProporcional { get; private set; }

    // Vínculo com produto cadastrado
    public Guid? ProdutoId { get; private set; }
    public string? ProdutoDescricao { get; private set; }

    // Conversão de unidade (ex: caixa → kg)
    public decimal FatorConversao { get; private set; } = 1m;      // qtd xml × fator = qtd estoque
    public string? UnidadeEstoque { get; private set; }
    public decimal QuantidadeEstoque => QuantidadeXml * FatorConversao;

    // Custo com rateio de frete/seguro/IPI
    public decimal CustoUnitarioFinal { get; private set; }

    // Estoque
    public Guid? LoteId { get; private set; }
    public string? NumeroLote { get; private set; }
    public DateTime? Validade { get; private set; }
    public bool EstoqueMovimentado { get; private set; }

    // Tags (para impressão de etiqueta)
    public string? Tags { get; private set; }

    // Formação de preço
    public decimal? PrecoVendaSugerido { get; private set; }
    public decimal? MarkupSugerido { get; private set; }

    private ItemEntradaNFe() { }

    public static ItemEntradaNFe Criar(
        Guid entradaId, int numero, string cfop, string ncm, string descricao,
        decimal quantidade, string unidade, decimal valorUnitario, decimal valorTotal,
        string? codigoFornecedor = null, string? codigoBarras = null,
        decimal valIpi = 0, decimal valIcmsSt = 0)
        => new()
        {
            EntradaNFeId = entradaId,
            NumeroItem = numero,
            CfopXml = cfop,
            CfopUtilizado = cfop,
            NcmXml = ncm,
            DescricaoXml = descricao,
            QuantidadeXml = quantidade,
            UnidadeXml = unidade,
            ValorUnitarioXml = valorUnitario,
            ValorTotalXml = valorTotal,
            CodigoFornecedor = codigoFornecedor,
            CodigoBarras = codigoBarras,
            ValorIpi = valIpi,
            ValorIcmsSt = valIcmsSt,
            FatorConversao = 1m,
        };

    public void VincularProduto(Guid produtoId, string descricao)
    {
        ProdutoId = produtoId;
        ProdutoDescricao = descricao;
    }

    public void DefinirConversao(decimal fator, string unidadeEstoque)
    {
        FatorConversao = fator;
        UnidadeEstoque = unidadeEstoque;
    }

    public void DefinirCfop(string cfop) => CfopUtilizado = cfop;

    public void DefinirLote(string numero, DateTime? validade, Guid? loteId = null)
    {
        NumeroLote = numero;
        Validade = validade;
        LoteId = loteId;
    }

    public void DefinirFreteProporcional(decimal valor) => ValorFreteProporcional = valor;

    public void CalcularCusto(decimal freteProporcional)
    {
        ValorFreteProporcional = freteProporcional;
        var totalItem = ValorTotalXml + ValorIpi + ValorIcmsSt + freteProporcional;
        CustoUnitarioFinal = QuantidadeEstoque > 0 ? totalItem / QuantidadeEstoque : ValorUnitarioXml;
    }

    public void SugerirPreco(decimal markup)
    {
        MarkupSugerido = markup;
        PrecoVendaSugerido = CustoUnitarioFinal * markup;
    }

    public void DefinirTags(string tags) => Tags = tags;

    public void MarcarEstoqueMovimentado() => EstoqueMovimentado = true;
}

public enum StatusEntradaNFe
{
    EmEdicao,
    Processada,
    Estornada,
}

/// <summary>Método de rateio do frete entre os itens da entrada.</summary>
public enum MetodoRateioFrete
{
    ValorProporcional = 0,   // padrão: proporcional ao valor dos produtos
    // Futuro: Peso, Quantidade, Volume
}
