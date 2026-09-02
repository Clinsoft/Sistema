using FluentAssertions;
using Sistema.Domain.Fiscal.Entities;

namespace Sistema.UnitTests.Domain.Fiscal;

/// <summary>
/// Regras de custo/preço na escrituração de entrada. Cobre o bug do "preço de
/// caixa fechada": um item comprado em caixa deve, com o fator de conversão,
/// resultar em custo e preço POR UNIDADE — não pelo valor da caixa inteira.
/// </summary>
public class EntradaNFeTests
{
    private static EntradaNFe CriarEntradaComItem(
        decimal quantidadeXml, decimal valorTotalXml, decimal fator, string unidade = "UN")
    {
        var entrada = EntradaNFe.Criar(
            Guid.NewGuid(), Guid.NewGuid(), new string('1', 44), "Fornecedor X", "00000000000191",
            DateTime.Today, Guid.NewGuid(),
            valProdutos: valorTotalXml, valFrete: 0, valSeguro: 0,
            valDesconto: 0, valIpi: 0, valIcmsSt: 0, valTotal: valorTotalXml);

        var item = ItemEntradaNFe.Criar(
            entrada.Id, 1, "5102", "21069090", "BENDU PACOCA 20G",
            quantidadeXml, "CX", valorUnitario: valorTotalXml / quantidadeXml, valorTotal: valorTotalXml);
        entrada.AdicionarItem(item);

        item.DefinirConversao(fator, unidade);
        entrada.RatearFrete();   // recalcula CustoUnitarioFinal com a quantidade convertida
        return entrada;
    }

    [Fact]
    public void FatorConversao_DeveConverterQuantidadeParaUnidades()
    {
        // 1 caixa com fator 12 → 12 unidades em estoque
        var entrada = CriarEntradaComItem(quantidadeXml: 1, valorTotalXml: 240m, fator: 12m);
        var item = entrada.Itens.Single();

        item.QuantidadeEstoque.Should().Be(12m);
    }

    [Fact]
    public void CustoUnitario_DeveSerPorUnidade_NaoPorCaixa()
    {
        // Caixa de R$ 240 com 12 un → custo unitário R$ 20 (não R$ 240)
        var entrada = CriarEntradaComItem(quantidadeXml: 1, valorTotalXml: 240m, fator: 12m);
        var item = entrada.Itens.Single();

        item.CustoUnitarioFinal.Should().Be(20m);
    }

    [Fact]
    public void SugerirPreco_DeveUsarCustoPorUnidade()
    {
        // custo/un = 20; markup 2.0 → preço sugerido 40 (não 480 da caixa)
        var entrada = CriarEntradaComItem(quantidadeXml: 1, valorTotalXml: 240m, fator: 12m);
        var item = entrada.Itens.Single();

        item.SugerirPreco(2.0m);

        item.PrecoVendaSugerido.Should().Be(40m);
    }

    [Fact]
    public void SemConversao_Fator1_CustoUnitarioEhTotalDivididoPelaQuantidade()
    {
        // 6 unidades por R$ 30 (fator 1) → custo unitário R$ 5
        var entrada = CriarEntradaComItem(quantidadeXml: 6, valorTotalXml: 30m, fator: 1m);
        var item = entrada.Itens.Single();

        item.QuantidadeEstoque.Should().Be(6m);
        item.CustoUnitarioFinal.Should().Be(5m);
    }
}
