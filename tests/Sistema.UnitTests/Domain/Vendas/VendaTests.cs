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
    public void AdicionarItem_ComDesconto_DeveReduzirTotal()
    {
        var v = CriarVenda();
        v.AdicionarItem(ProdutoId, "Omega 3", 1, 100m, desconto: 10m);
        v.Total.Should().Be(90m);
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
