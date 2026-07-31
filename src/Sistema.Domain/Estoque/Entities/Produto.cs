using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class Produto : Entity
{
    public Guid EmpresaId { get; private set; }

    // Identificação
    public string Codigo { get; private set; } = null!;
    public string? Referencia { get; private set; }
    public string Descricao { get; private set; } = null!;
    public string? DescricaoComplementar { get; private set; }
    public string? CodigoBarras { get; private set; }

    // Vínculos
    public Guid CategoriaId { get; private set; }
    public Guid MarcaId { get; private set; }
    public Guid UnidadeMedidaId { get; private set; }
    public Guid? FornecedorPrincipalId { get; private set; }
    public string? CodigoFornecedorPrincipal { get; private set; }  // código deste produto no fornecedor (de-para p/ entrada NF-e)

    // Variação
    public string TipoVariacao { get; private set; } = "Simples"; // Simples | ComVariacao

    // Flags de comportamento
    public bool ProdutoBalanca { get; private set; }
    public int? CodigoPlu { get; private set; }
    public bool OcultarNasVendas { get; private set; }
    public bool RequisitarVendedor { get; private set; }
    public bool VendidoFracionado { get; private set; }
    public bool Ativo { get; private set; } = true;

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
    public string Origem { get; private set; } = "0";
    public string? CodigoFci { get; private set; }

    // Preços
    public decimal PrecoFornecedor { get; private set; }
    public decimal CustoUnitario { get; private set; }
    public decimal MarkupMinimo { get; private set; }
    public decimal PrecoMinimo { get; private set; }
    public decimal PrecoVenda { get; private set; }
    public decimal? PrecoAtacado { get; private set; }
    public decimal? MarkupAtacado { get; private set; }
    public decimal Markup { get; private set; }
    public decimal MargemLucro { get; private set; }

    // Estoque
    public decimal EstoqueAtual { get; private set; }
    public decimal EstoqueMinimo { get; private set; }
    public decimal EstoqueMaximo { get; private set; }
    public bool ControlarLote { get; private set; }
    public bool ControlarValidade { get; private set; }
    public int? ValidadeEmDias { get; private set; }

    // Etiqueta: fica "desatualizada" quando o preço muda (produtos de balança/peso),
    // sinalizando que é preciso reimprimir a etiqueta antes da próxima venda.
    public bool EtiquetaDesatualizada { get; private set; }

    /// <summary>Produto vendido por peso (balança ou fracionado).</summary>
    public bool VendidoPorPeso => ProdutoBalanca || VendidoFracionado;

    /// <summary>Marca que a etiqueta foi (re)impressa e está atualizada.</summary>
    public void MarcarEtiquetaImpressa() => EtiquetaDesatualizada = false;
    public void MarcarEtiquetaDesatualizada() => EtiquetaDesatualizada = true;

    // Balança: produto de peso fica "pendente" quando muda o preço ou quando vira produto
    // de balança/peso. Obriga reexportar o arquivo para a balança antes de vender no peso certo.
    public bool BalancaDesatualizada { get; private set; }

    /// <summary>Marca que o arquivo foi (re)exportado para a balança e está atualizado.</summary>
    public void MarcarBalancaEnviada() => BalancaDesatualizada = false;
    /// <summary>Marca pendência de envio à balança (só faz sentido em produto de peso).</summary>
    public void MarcarBalancaDesatualizada()
    {
        if (VendidoPorPeso) BalancaDesatualizada = true;
    }

    // Imagem e informações adicionais
    public string? ImagemUrl { get; private set; }
    public string? FichaTecnicaUrl { get; private set; }
    public string? Marcador { get; private set; }
    public string? Tags { get; private set; }
    public string? InformacaoAdicional { get; private set; }

    // Navigation
    private readonly List<ProdutoEmbalagem> _embalagens = [];
    public IReadOnlyList<ProdutoEmbalagem> Embalagens => _embalagens.AsReadOnly();

    private Produto() { }

    public static Produto Criar(Guid empresaId, string codigo, string descricao,
        Guid categoriaId, Guid marcaId, Guid unidadeMedidaId,
        decimal custoUnitario, decimal precoVenda,
        string? codigoBarras = null)
    {
        var markup = custoUnitario > 0 ? Math.Round(precoVenda / custoUnitario, 4) : 0;
        var margem = precoVenda > 0 ? Math.Round((precoVenda - custoUnitario) / precoVenda * 100, 2) : 0;

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
            Markup = markup,
            MargemLucro = margem,
        };
    }

    public void EditarGeral(string descricao, string? referencia, Guid categoriaId, Guid marcaId,
        Guid unidadeMedidaId, Guid? fornecedorId, string tipoVariacao,
        bool produtoBalanca, int? codigoPlu, bool ocultarNasVendas,
        bool requisitarVendedor, bool vendidoFracionado, bool ativo,
        bool controlarLote, bool controlarValidade, int? validadeEmDias,
        string? descricaoComplementar)
    {
        var eraPeso = VendidoPorPeso;
        Descricao = descricao;
        Referencia = referencia;
        CategoriaId = categoriaId;
        MarcaId = marcaId;
        UnidadeMedidaId = unidadeMedidaId;
        FornecedorPrincipalId = fornecedorId;
        TipoVariacao = tipoVariacao;
        ProdutoBalanca = produtoBalanca;
        CodigoPlu = codigoPlu;
        OcultarNasVendas = ocultarNasVendas;
        RequisitarVendedor = requisitarVendedor;
        VendidoFracionado = vendidoFracionado;
        Ativo = ativo;
        ControlarLote = controlarLote;
        ControlarValidade = controlarValidade;
        ValidadeEmDias = validadeEmDias;
        DescricaoComplementar = descricaoComplementar;
        // Virou produto de peso agora → precisa exportar para a balança.
        if (!eraPeso && VendidoPorPeso) BalancaDesatualizada = true;
    }

    /// <summary>Atualiza o código de barras (EAN/GTIN) do produto.</summary>
    public void AtualizarCodigoBarras(string? codigoBarras)
        => CodigoBarras = string.IsNullOrWhiteSpace(codigoBarras) ? null : codigoBarras.Trim();

    /// <summary>Altera o código interno do produto (é o PLU usado na balança quando não há CodigoPlu).</summary>
    public void EditarCodigo(string codigo)
    {
        if (!string.IsNullOrWhiteSpace(codigo))
            Codigo = codigo.Trim();
    }

    /// <summary>Registra o de-para com o fornecedor: código do produto na nota do fornecedor.</summary>
    public void VincularReferenciaFornecedor(Guid fornecedorId, string? codigoNoFornecedor)
    {
        FornecedorPrincipalId = fornecedorId;
        if (!string.IsNullOrWhiteSpace(codigoNoFornecedor))
            CodigoFornecedorPrincipal = codigoNoFornecedor;
    }

    public void EditarPrecos(decimal precoFornecedor, decimal custoUnitario,
        decimal markupMinimo, decimal precoMinimo,
        decimal precoVenda, decimal? precoAtacado, decimal? markupAtacado)
    {
        if (precoVenda != PrecoVenda) { EtiquetaDesatualizada = true; if (VendidoPorPeso) BalancaDesatualizada = true; }
        PrecoFornecedor = precoFornecedor;
        CustoUnitario = custoUnitario;
        MarkupMinimo = markupMinimo;
        PrecoMinimo = precoMinimo;
        PrecoVenda = precoVenda;
        PrecoAtacado = precoAtacado;
        MarkupAtacado = markupAtacado;
        Markup = custoUnitario > 0 ? Math.Round(precoVenda / custoUnitario, 4) : 0;
        MargemLucro = precoVenda > 0 ? Math.Round((precoVenda - custoUnitario) / precoVenda * 100, 2) : 0;
    }

    // NCM/CEST/CFOP vão no XML fiscal só com DÍGITOS (sem pontos). O usuário pode
    // digitar "1904.90.00" — normalizamos para "19049000" e evitamos estourar a coluna.
    private static string? SoDigitos(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var d = new string(s.Where(char.IsDigit).ToArray());
        if (d.Length == 0) return null;
        return d.Length > max ? d[..max] : d;
    }

    public void EditarFiscal(string? ncm, string? cest, string? cstIcms, string? csosnIcms,
        string? cstPisCofins, decimal aliquotaIcms, decimal aliquotaPis, decimal aliquotaCofins,
        string? cfop, string origem, string? codigoFci)
    {
        Ncm = SoDigitos(ncm, 8);
        Cest = SoDigitos(cest, 7);
        CstIcms = cstIcms;
        CsosnIcms = csosnIcms;
        CstPisCofins = cstPisCofins;
        AliquotaIcms = aliquotaIcms;
        AliquotaPis = aliquotaPis;
        AliquotaCofins = aliquotaCofins;
        Cfop = SoDigitos(cfop, 4) ?? cfop;
        Origem = origem;
        CodigoFci = codigoFci;
    }

    public void EditarInfoAdicional(string? imagemUrl, string? marcador, string? tags,
        string? informacaoAdicional)
    {
        ImagemUrl = imagemUrl;
        Marcador = marcador;
        Tags = tags;
        InformacaoAdicional = informacaoAdicional;
    }

    public void DefinirFichaTecnica(string? url) => FichaTecnicaUrl = url;

    // Compatibilidade com chamadas legadas
    public void Editar(string descricao, Guid categoriaId, Guid marcaId, Guid unidadeMedidaId,
        string? codigoBarras, string? ncm, decimal estoqueMinimo, bool ativo)
    {
        Descricao = descricao;
        CategoriaId = categoriaId;
        MarcaId = marcaId;
        UnidadeMedidaId = unidadeMedidaId;
        Ncm = ncm;
        EstoqueMinimo = estoqueMinimo;
        Ativo = ativo;
    }

    public void AtualizarPreco(decimal novoCusto, decimal novoPreco)
    {
        if (novoPreco != PrecoVenda) { EtiquetaDesatualizada = true; if (VendidoPorPeso) BalancaDesatualizada = true; }
        CustoUnitario = novoCusto;
        PrecoVenda = novoPreco;
        Markup = novoCusto > 0 ? Math.Round(novoPreco / novoCusto, 4) : 0;
        MargemLucro = novoPreco > 0 ? Math.Round((novoPreco - novoCusto) / novoPreco * 100, 2) : 0;
    }

    public void AtualizarPrecoEMarkup(decimal precoVenda, decimal markup)
    {
        if (precoVenda != PrecoVenda) { EtiquetaDesatualizada = true; if (VendidoPorPeso) BalancaDesatualizada = true; }
        PrecoVenda = precoVenda;
        Markup = markup;
        MargemLucro = precoVenda > 0 ? Math.Round((precoVenda - CustoUnitario) / precoVenda * 100, 2) : 0;
    }

    /// <summary>
    /// Aplica tributação padrão conforme regime tributário da empresa.
    /// Não sobrescreve campos que já foram definidos individualmente (NCM, CEST).
    /// </summary>
    public void AplicarTributacaoPadrao(string regime)
    {
        switch (regime)
        {
            case "SimplesNacional":
                // Padrão do contador: CFOP 5102 (venda normal) → CSOSN 102.
                // Produtos com ST (CFOP 5405 → CSOSN 500) são exceção, ajustada por produto.
                CsosnIcms      = "102";
                CstIcms        = null;
                AliquotaIcms   = 0m;
                CstPisCofins   = "07";
                AliquotaPis    = 0m;
                AliquotaCofins = 0m;
                Cfop           ??= "5102";
                break;

            case "LucroPresumido":
                CstIcms        = "000";
                CsosnIcms      = null;
                AliquotaIcms   = 12m;   // interestadual; ajustar por UF se necessário
                CstPisCofins   = "01";  // cumulativo
                AliquotaPis    = 0.65m;
                AliquotaCofins = 3m;
                Cfop           ??= "5102";
                break;

            case "LucroReal":
                CstIcms        = "000";
                CsosnIcms      = null;
                AliquotaIcms   = 12m;
                CstPisCofins   = "02";  // não cumulativo
                AliquotaPis    = 1.65m;
                AliquotaCofins = 7.6m;
                Cfop           ??= "5102";
                break;
        }
    }

    /// <summary>
    /// Prepara o produto para a balança. Aplicado automaticamente a todo produto
    /// com unidade pesável (KG). O PLU é o número que identifica o produto na
    /// balança: usa o código interno quando ainda não houver um definido.
    /// </summary>
    public void MarcarComoBalanca(int? pluSugerido = null)
    {
        ProdutoBalanca = true;
        CodigoPlu ??= pluSugerido ?? (int.TryParse(Codigo, out var c) ? c : null);
    }

    /// <summary>
    /// Liga o controle de validade com um prazo padrão, sem sobrescrever um prazo
    /// já informado. Usado ao cadastrar produto de balança — a validade é o que a
    /// balança soma à data de embalagem ao imprimir a etiqueta. O usuário pode
    /// depois alterar o prazo ou desligar o controle.
    /// </summary>
    public void AplicarValidadePadrao(int dias)
    {
        ControlarValidade = true;
        ValidadeEmDias ??= dias;
    }

    public void AjustarEstoque(decimal quantidade) => EstoqueAtual += quantidade;
    public void DefinirEstoqueMinimo(decimal minimo) => EstoqueMinimo = minimo;
    public bool EstoqueAbaixoDoMinimo() => EstoqueAtual <= EstoqueMinimo;
    public void Desativar() => Ativo = false;

    public void EntradaEstoque(decimal quantidade, decimal custoUnitario)
    {
        if (EstoqueAtual + quantidade > 0)
            CustoUnitario = Math.Round(
                (EstoqueAtual * CustoUnitario + quantidade * custoUnitario)
                / (EstoqueAtual + quantidade), 4);
        EstoqueAtual += quantidade;
    }

    public void SaidaEstoque(decimal quantidade) =>
        EstoqueAtual = Math.Max(0, EstoqueAtual - quantidade);
}
