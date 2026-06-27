using FluentAssertions;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.UnitTests.Domain.Estoque;

public class ProdutoTests
{
    private static Produto CriarProduto(decimal custo = 8.50m, decimal preco = 25.90m)
        => Produto.Criar(Guid.NewGuid(), "VIT-C-1G", "Vitamina C 1g 60 Cápsulas",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), custo, preco, "7891234567890");

    [Fact]
    public void Criar_DeveInicializarComEstoqueZero()
    {
        var p = CriarProduto();
        p.EstoqueAtual.Should().Be(0);
        p.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Criar_DeveCalcularMarkupCorretamente()
    {
        var p = CriarProduto(10m, 30m);
        p.Markup.Should().Be(3m); // 30/10 = 3
    }

    [Fact]
    public void Criar_DeveCalcularMargemLucroCorretamente()
    {
        var p = CriarProduto(10m, 25m);
        // margem = (25-10)/25 * 100 = 60%
        p.MargemLucro.Should().BeApproximately(60m, 0.01m);
    }

    [Fact]
    public void AjustarEstoque_DeveAlterarEstoque()
    {
        var p = CriarProduto();
        p.AjustarEstoque(100m);
        p.EstoqueAtual.Should().Be(100m);
        p.AjustarEstoque(-30m);
        p.EstoqueAtual.Should().Be(70m);
    }

    [Fact]
    public void EstoqueAbaixoDoMinimo_QuandoAbaixo_DeveRetornarTrue()
    {
        var p = CriarProduto();
        p.AjustarEstoque(5m);
        p.DefinirEstoqueMinimo(10m);
        p.EstoqueAbaixoDoMinimo().Should().BeTrue();
    }

    [Fact]
    public void EstoqueAbaixoDoMinimo_QuandoAcima_DeveRetornarFalse()
    {
        var p = CriarProduto();
        p.AjustarEstoque(50m);
        p.DefinirEstoqueMinimo(10m);
        p.EstoqueAbaixoDoMinimo().Should().BeFalse();
    }

    [Fact]
    public void AtualizarPreco_DeveAlterarCustoEPrecoERecalcularMarkup()
    {
        var p = CriarProduto();
        p.AtualizarPreco(10m, 30m);
        p.CustoUnitario.Should().Be(10m);
        p.PrecoVenda.Should().Be(30m);
        p.Markup.Should().Be(3m);
        p.MargemLucro.Should().BeApproximately(66.67m, 0.01m);
    }

    [Fact]
    public void Desativar_DeveMudarStatusParaInativo()
    {
        var p = CriarProduto();
        p.Desativar();
        p.Ativo.Should().BeFalse();
    }

    [Fact]
    public void AtualizarPreco_CustoZero_DeveDefinirMarkupComoZero()
    {
        var p = CriarProduto();
        p.AtualizarPreco(0m, 30m);
        p.Markup.Should().Be(0m);
    }
}
