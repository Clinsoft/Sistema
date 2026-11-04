using FluentAssertions;
using Sistema.Domain.Assinaturas;

namespace Sistema.UnitTests.Domain.Assinaturas;

public class AssinaturaPagamentoTests
{
    private static readonly DateTime Vencimento = new(2026, 11, 10);

    private static Assinatura Paga(DateTime proximo, CicloCobranca ciclo = CicloCobranca.Mensal)
    {
        var a = Assinatura.CriarTrial(Guid.NewGuid(), PlanoAssinatura.Essencial, ciclo);
        a.RegistrarPagamento(proximo);
        return a;
    }

    [Fact]
    public void Estorno_DoUltimoPagamento_VoltaAoVencimentoDaCobranca()
    {
        var a = Paga(Vencimento.AddMonths(1));

        a.DesfazerPagamento(Vencimento).Should().BeTrue();

        a.ProximoVencimento.Should().Be(Vencimento);
        a.Situacao(Vencimento.AddDays(Assinatura.DiasToleranciaPadrao + 1)).Should().Be(SituacaoAssinatura.Bloqueada);
    }

    [Fact]
    public void Estorno_DePagamentoAntigo_NaoApagaPagamentoMaisNovo()
    {
        var a = Paga(Vencimento.AddMonths(2));   // o mês seguinte já foi pago

        a.DesfazerPagamento(Vencimento).Should().BeFalse();

        a.ProximoVencimento.Should().Be(Vencimento.AddMonths(2));
    }

    [Fact]
    public void Estorno_Repetido_NaoMudaDeNovo()
    {
        var a = Paga(Vencimento.AddMonths(1));
        a.DesfazerPagamento(Vencimento);

        a.DesfazerPagamento(Vencimento).Should().BeFalse();

        a.ProximoVencimento.Should().Be(Vencimento);
    }

    [Fact]
    public void Estorno_DeCobrancaFutura_NaoAdiantaOVencimento()
    {
        var a = Paga(Vencimento);

        a.DesfazerPagamento(Vencimento.AddMonths(1)).Should().BeFalse();

        a.ProximoVencimento.Should().Be(Vencimento);
    }

    [Fact]
    public void Estorno_NoCicloAnual_ConsideraOAnoPago()
    {
        var a = Paga(Vencimento.AddYears(1), CicloCobranca.Anual);

        a.DesfazerPagamento(Vencimento).Should().BeTrue();

        a.ProximoVencimento.Should().Be(Vencimento);
    }

    [Fact]
    public void Pagar_DepoisDoEstorno_LiberaDeNovo()
    {
        var a = Paga(Vencimento.AddMonths(1));
        a.DesfazerPagamento(Vencimento);

        a.RegistrarPagamento(a.ValidoAte(Vencimento));

        a.PodeUsar(Vencimento.AddDays(20)).Should().BeTrue();
    }
}
