using FluentAssertions;
using Sistema.Domain.Financeiro.Entities;

namespace Sistema.UnitTests.Domain.Financeiro;

public class ContaBancariaTests
{
    private static ContaBancaria CriarConta(decimal saldoInicial = 1000m)
        => ContaBancaria.Criar(Guid.NewGuid(), "Caixa Principal", TipoConta.Caixa, saldoInicial);

    [Fact]
    public void Criar_DeveInicializarComSaldoCorreto()
    {
        var conta = CriarConta(500m);
        conta.SaldoAtual.Should().Be(500m);
        conta.SaldoInicial.Should().Be(500m);
        conta.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Creditar_DeveAumentarSaldo()
    {
        var conta = CriarConta(500m);
        conta.Creditar(300m);
        conta.SaldoAtual.Should().Be(800m);
    }

    [Fact]
    public void Debitar_ComSaldoSuficiente_DeveReduzirSaldo()
    {
        var conta = CriarConta(500m);
        conta.Debitar(200m);
        conta.SaldoAtual.Should().Be(300m);
    }

    [Fact]
    public void AjustarSaldo_DeveDefinirSaldoExato()
    {
        var conta = CriarConta(1000m);
        conta.AjustarSaldo(1234.56m);
        conta.SaldoAtual.Should().Be(1234.56m);
    }

    [Fact]
    public void Desativar_DeveMarcarContaComoInativa()
    {
        var conta = CriarConta();
        conta.Desativar();
        conta.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Creditar_MultiplasCreditacoes_DeveSomar()
    {
        var conta = CriarConta(0m);
        conta.Creditar(100m);
        conta.Creditar(200m);
        conta.Creditar(50m);
        conta.SaldoAtual.Should().Be(350m);
    }

    [Fact]
    public void Debitar_SaldoFicaNegativo_DevePermitir()
    {
        // ContaBancaria não valida saldo negativo (limite de cheque especial, etc.)
        var conta = CriarConta(100m);
        conta.Debitar(500m);
        conta.SaldoAtual.Should().Be(-400m);
    }
}
