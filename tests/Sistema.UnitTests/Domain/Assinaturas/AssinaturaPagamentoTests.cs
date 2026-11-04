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
    public void Diferenca_AoSubirDePlano_CobraSoOsDiasQueFaltam()
    {
        var a = Paga(new DateTime(2026, 12, 10));   // período de 30 dias, faltam 20

        a.DiferencaProporcional(PlanoAssinatura.Profissional, new DateTime(2026, 11, 20)).Should().Be(73.33m);   // (229 - 119) * 20 / 30
    }

    [Fact]
    public void Diferenca_NoAnual_UsaOPrecoEOPeriodoDoAno()
    {
        var a = Paga(new DateTime(2027, 11, 10), CicloCobranca.Anual);   // ano de 365 dias, faltam 182

        a.DiferencaProporcional(PlanoAssinatura.Profissional, new DateTime(2027, 5, 12)).Should().Be(548.49m);   // (2290 - 1190) * 182 / 365
    }

    [Theory]
    [InlineData(PlanoAssinatura.Micro)]        // desceu: sem crédito
    [InlineData(PlanoAssinatura.Essencial)]    // mesmo plano
    public void Diferenca_SemSubirDePlano_EZero(PlanoAssinatura novo)
    {
        var a = Paga(new DateTime(2026, 12, 10));

        a.DiferencaProporcional(novo, new DateTime(2026, 11, 20)).Should().Be(0m);
    }

    [Theory]
    [InlineData(2026, 12, 10)]   // vence hoje: a cobrança de hoje já vem com o valor novo
    [InlineData(2026, 12, 20)]   // vencido: não há dias pagos pela frente
    public void Diferenca_SemDiasPagosPelaFrente_EZero(int ano, int mes, int dia)
    {
        var a = Paga(new DateTime(2026, 12, 10));
        var hoje = new DateTime(ano, mes, dia);

        a.PagoAte(hoje).Should().BeNull();
        a.DiferencaProporcional(PlanoAssinatura.Profissional, hoje).Should().Be(0m);
    }

    [Fact]
    public void Diferenca_NoTesteGratis_EZero()
    {
        var a = Assinatura.CriarTrial(Guid.NewGuid(), PlanoAssinatura.Essencial);

        a.DiferencaProporcional(PlanoAssinatura.Profissional, DateTime.Today).Should().Be(0m);
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
