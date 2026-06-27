using FluentAssertions;
using Sistema.Domain.Financeiro.Entities;

namespace Sistema.UnitTests.Domain.Financeiro;

public class LancamentoFinanceiroTests
{
    private static LancamentoFinanceiro CriarLancamento(decimal valor = 500m, int diasVencimento = 30)
        => LancamentoFinanceiro.Criar(
            Guid.NewGuid(), TipoLancamento.ContaReceber,
            "Venda #001", valor,
            DateTime.Today.AddDays(diasVencimento));

    [Fact]
    public void Criar_DeveInicializarComStatusEmAberto()
    {
        var l = CriarLancamento();
        l.Status.Should().Be(StatusLancamento.EmAberto);
        l.ValorPago.Should().Be(0);
        l.Saldo.Should().Be(500m);
    }

    [Fact]
    public void Baixar_ComValorTotal_DeveMarcarComoPago()
    {
        var l = CriarLancamento(1000m);
        l.Baixar(1000m, DateTime.Today);
        l.Status.Should().Be(StatusLancamento.Pago);
        l.ValorPago.Should().Be(1000m);
        l.Saldo.Should().Be(0);
    }

    [Fact]
    public void Baixar_ComValorParcial_DeveMarcarComoPagoParcialmente()
    {
        var l = CriarLancamento(1000m);
        l.Baixar(300m, DateTime.Today);
        l.Status.Should().Be(StatusLancamento.PagoParcialmente);
        l.ValorPago.Should().Be(300m);
        l.Saldo.Should().Be(700m);
    }

    [Fact]
    public void Baixar_LancamentoJaPago_DeveLancarExcecao()
    {
        var l = CriarLancamento();
        l.Baixar(500m, DateTime.Today);
        var act = () => l.Baixar(500m, DateTime.Today);
        act.Should().Throw<InvalidOperationException>().WithMessage("*pago*");
    }

    [Fact]
    public void Cancelar_LancamentoPago_DeveLancarExcecao()
    {
        var l = CriarLancamento();
        l.Baixar(500m, DateTime.Today);
        var act = () => l.Cancelar();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancelar_LancamentoEmAberto_DeveAlterarParaCancelado()
    {
        var l = CriarLancamento();
        l.Cancelar();
        l.Status.Should().Be(StatusLancamento.Cancelado);
    }

    [Fact]
    public void Renegociar_DeveAlterarValorEVencimento()
    {
        var l = CriarLancamento(500m);
        var novoVenc = DateTime.Today.AddDays(60);
        l.Renegociar(700m, novoVenc, "acréscimo de juros");
        l.ValorOriginal.Should().Be(700m);
        l.DataVencimento.Should().Be(novoVenc);
        l.Status.Should().Be(StatusLancamento.EmAberto);
        l.Observacao.Should().Be("acréscimo de juros");
    }

    [Fact]
    public void Vencido_LancamentoVencidoEmAberto_DeveRetornarTrue()
    {
        var l = LancamentoFinanceiro.Criar(
            Guid.NewGuid(), TipoLancamento.ContaPagar,
            "Aluguel", 1500m, DateTime.Today.AddDays(-1));
        l.Vencido.Should().BeTrue();
    }

    [Fact]
    public void Vencido_LancamentoPago_DeveRetornarFalse()
    {
        var l = CriarLancamento(-5);
        l.Baixar(500m, DateTime.Today);
        l.Vencido.Should().BeFalse();
    }

    [Fact]
    public void Vencido_LancamentoFuturo_DeveRetornarFalse()
    {
        var l = CriarLancamento(diasVencimento: 30);
        l.Vencido.Should().BeFalse();
    }

    [Fact]
    public void Saldo_DeveReflecirPagamentoParcial()
    {
        var l = CriarLancamento(400m);
        l.Baixar(100m, DateTime.Today);
        l.Saldo.Should().Be(300m);
    }
}
