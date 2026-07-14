using FluentAssertions;
using Sistema.Domain.Fiscal.Entities;

namespace Sistema.FiscalTests;

/// <summary>
/// Rateio de frete PROPORCIONAL AO VALOR dos produtos na entrada de NF-e.
/// </summary>
public class RateioFreteTests
{
    private static EntradaNFe CriarEntrada(decimal frete, decimal valorProdutos)
        => EntradaNFe.Criar(
            Guid.NewGuid(), Guid.NewGuid(), "chave", "Fornecedor", "00000000000000",
            DateTime.Today, Guid.NewGuid(),
            valProdutos: valorProdutos, valFrete: frete, valSeguro: 0,
            valDesconto: 0, valIpi: 0, valIcmsSt: 0, valTotal: valorProdutos + frete);

    private static ItemEntradaNFe CriarItem(Guid entradaId, int n, decimal qtd, decimal valorTotal)
        => ItemEntradaNFe.Criar(entradaId, n, "1102", "00000000", $"Produto {n}",
            quantidade: qtd, unidade: "UN", valorUnitario: valorTotal / qtd, valorTotal: valorTotal);

    [Fact]
    public void RatearFrete_Proporcional_DistribuiPeloValorEAtualizaCustoUnitario()
    {
        // Exemplo do requisito: frete R$100; Produto A (10un, R$700) e B (5un, R$300).
        var entrada = CriarEntrada(frete: 100m, valorProdutos: 1000m);
        var a = CriarItem(entrada.Id, 1, qtd: 10m, valorTotal: 700m);
        var b = CriarItem(entrada.Id, 2, qtd: 5m, valorTotal: 300m);
        entrada.AdicionarItem(a);
        entrada.AdicionarItem(b);

        entrada.RatearFrete();

        // Produto A: frete 70, custo total 770, custo unitário 77
        a.ValorFreteProporcional.Should().Be(70m);
        a.CustoUnitarioFinal.Should().Be(77m);

        // Produto B: frete 30, custo total 330, custo unitário 66
        b.ValorFreteProporcional.Should().Be(30m);
        b.CustoUnitarioFinal.Should().Be(66m);

        // A soma do frete rateado é exatamente o frete total.
        (a.ValorFreteProporcional + b.ValorFreteProporcional).Should().Be(100m);
    }

    [Fact]
    public void RatearFrete_AjustaArredondamentoNoUltimoItem()
    {
        // Frete que não divide "redondo" → o último item recebe o resto.
        var entrada = CriarEntrada(frete: 10m, valorProdutos: 300m);
        var a = CriarItem(entrada.Id, 1, qtd: 1m, valorTotal: 100m);
        var b = CriarItem(entrada.Id, 2, qtd: 1m, valorTotal: 100m);
        var c = CriarItem(entrada.Id, 3, qtd: 1m, valorTotal: 100m);
        entrada.AdicionarItem(a);
        entrada.AdicionarItem(b);
        entrada.AdicionarItem(c);

        entrada.RatearFrete();

        // 10 × (100/300) = 3,3333 → arredonda 3,33 nos dois primeiros; último = 10 - 6,66 = 3,34
        a.ValorFreteProporcional.Should().Be(3.33m);
        b.ValorFreteProporcional.Should().Be(3.33m);
        c.ValorFreteProporcional.Should().Be(3.34m);
        (a.ValorFreteProporcional + b.ValorFreteProporcional + c.ValorFreteProporcional)
            .Should().Be(10m);
    }

    [Fact]
    public void RatearFrete_SemFrete_CustoIgualAoValorUnitario()
    {
        var entrada = CriarEntrada(frete: 0m, valorProdutos: 500m);
        var item = CriarItem(entrada.Id, 1, qtd: 5m, valorTotal: 500m);
        entrada.AdicionarItem(item);

        entrada.RatearFrete();

        item.ValorFreteProporcional.Should().Be(0m);
        item.CustoUnitarioFinal.Should().Be(100m); // 500 / 5
    }
}
