using FluentAssertions;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.UnitTests.Domain.Vendas;

public class VendaTests
{
    private static readonly Guid EmpresaId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid LocalEstoqueId = Guid.NewGuid();
    private static readonly Guid ProdutoId = Guid.NewGuid();
    private static int _seq = 1;

    private static Venda CriarVenda()
        => Venda.Iniciar(EmpresaId, UsuarioId, LocalEstoqueId, $"VDA-{_seq++:D6}");

    [Fact]
    public void Iniciar_DeveGerarNumeroNaoVazio()
    {
        var v = CriarVenda();
        v.Numero.Should().NotBeNullOrEmpty();
        v.Status.Should().Be(StatusVenda.EmAberto);
        v.Total.Should().Be(0);
    }

    [Fact]
    public void AdicionarItem_DeveCalcularTotalCorretamente()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Vitamina C 1g", 3, 25.90m);
        v.Total.Should().Be(3 * 25.90m);
        v.Itens.Should().HaveCount(1);
    }

    [Fact]
    public void AdicionarItem_ComDescontoPercentual_DeveReduzirTotal()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Omega 3", 1, 100m, percentualDesconto: 10m);
        v.Total.Should().Be(90m);
    }

    [Fact]
    public void AdicionarItem_ComDescontoEmReais_BateAoCentavo_ItemPorKg()
    {
        // Promoção "menor valor 30%" sobre item por kg: o desconto vai em REAIS (absoluto),
        // então o Total do backend bate EXATAMENTE com o total exibido no PDV (sem
        // divergência de arredondamento que causava "pagamento insuficiente").
        var v = CriarVenda();
        // Amendoim: 42,23/kg × 0,174 = 7,348 → item bruto R$ 7,35; promo 30% ≈ R$ 2,20.
        v.AdicionarItem(ProdutoId, "Amendoim churrasco", 0.174m, 42.23m, descontoValor: 2.20m);
        v.Itens[0].Total.Should().Be(5.15m);   // 7,35 − 2,20
        v.Total.Should().Be(5.15m);
    }

    [Fact]
    public void AdicionarItem_DescontoEmReais_NuncaDeixaTotalNegativo()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Item barato", 1, 5m, descontoValor: 999m);
        v.Total.Should().Be(0m);
    }

    [Fact]
    public void RemoverItem_DeveDiminuirTotal()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Vitamina C 1g", 2, 25.90m);
        var itemId = v.Itens[0].Id;
        v.RemoverItem(itemId);
        v.Total.Should().Be(0);
        v.Itens.Should().BeEmpty();
    }

    [Fact]
    public void RemoverItem_IdInexistente_DeveLancarExcecao()
    {
        var v = CriarVenda();
        var act = () => v.RemoverItem(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AdicionarPagamento_DeveAcumularValorPago()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Spirulina", 1, 80m);
        v.AdicionarPagamento(FormaPagamento.Dinheiro, 40m);
        v.AdicionarPagamento(FormaPagamento.Pix, 40m);
        v.TotalPago.Should().Be(80m);
    }

    [Fact]
    public void AdicionarPagamento_ValorMaiorQueTotal_DeveCalcularTroco()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Chá Verde", 1, 30m);
        v.AdicionarPagamento(FormaPagamento.Dinheiro, 50m);
        v.Troco.Should().Be(20m);
    }

    [Fact]
    public void Finalizar_ComPagamentoCompleto_DeveAlterarStatus()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Colágeno", 2, 50m);
        v.AdicionarPagamento(FormaPagamento.Pix, 100m);
        v.Finalizar();
        v.Status.Should().Be(StatusVenda.Finalizada);
        v.DataHoraFechamento.Should().NotBeNull();
    }

    [Fact]
    public void Finalizar_SemItens_DeveLancarExcecao()
    {
        var v = CriarVenda();
        var act = () => v.Finalizar();
        act.Should().Throw<InvalidOperationException>().WithMessage("*itens*");
    }

    [Fact]
    public void Finalizar_PagamentoInsuficiente_DeveLancarExcecao()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Chia", 1, 50m);
        v.AdicionarPagamento(FormaPagamento.Dinheiro, 30m);
        var act = () => v.Finalizar();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Pagamento*");
    }

    [Fact]
    public void Cancelar_VendaEmAberto_DeveAlterarParaCancelada()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Proteína", 1, 120m);
        v.Cancelar("cliente desistiu");
        v.Status.Should().Be(StatusVenda.Cancelada);
        v.Observacao.Should().Be("cliente desistiu");
    }

    [Fact]
    public void Cancelar_VendaCancelada_NaoDeveLancarExcecao()
    {
        var v = CriarVenda();
        v.Cancelar("primeiro");
        var act = () => v.Cancelar("segundo");
        act.Should().NotThrow(); // idempotente
    }
}
