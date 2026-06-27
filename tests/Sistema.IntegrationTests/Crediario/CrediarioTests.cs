using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sistema.Application.Crediario.Commands;
using Sistema.Domain.Crediario.Entities;
using Sistema.Infrastructure.Data;
using Sistema.IntegrationTests.Infrastructure;

namespace Sistema.IntegrationTests.Crediario;

public class CrediarioTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IMediator _mediator;
    private readonly SistemaDbContext _db;

    private static readonly Guid EmpresaId = Guid.NewGuid();
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();

    public CrediarioTests()
    {
        var provider = TestServiceProvider.Criar();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        _db = _scope.ServiceProvider.GetRequiredService<SistemaDbContext>();
    }

    private Task<AbrirCrediarioResult> AbrirCrediario(
        decimal total = 1000m, decimal entrada = 0m,
        int parcelas = 5, decimal juros = 2m) =>
        _mediator.Send(new AbrirCrediarioCommand(
            EmpresaId, ClienteId, UsuarioId,
            total, entrada, parcelas, juros));

    [Fact]
    public async Task AbrirCrediario_DeveGerarParcelasCorretas()
    {
        var resultado = await AbrirCrediario(total: 1000m, parcelas: 5, juros: 0m);

        resultado.NumeroParcelas.Should().Be(5);
        resultado.Numero.Should().NotBeNullOrEmpty();

        var crediarioDB = await _db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == resultado.Id);

        crediarioDB!.Parcelas.Should().HaveCount(5);
        crediarioDB.Parcelas.Sum(p => p.Valor).Should().BeApproximately(1000m, 0.05m);
    }

    [Fact]
    public async Task AbrirCrediario_ComEntrada_DeveReduzirValorFinanciado()
    {
        var resultado = await AbrirCrediario(total: 1000m, entrada: 200m, parcelas: 4, juros: 0m);

        var crediarioDB = await _db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == resultado.Id);

        crediarioDB!.ValorFinanciado.Should().Be(800m);
        crediarioDB.Parcelas.Should().HaveCount(4);
        crediarioDB.Parcelas.Sum(p => p.Valor).Should().BeApproximately(800m, 0.05m);
    }

    [Fact]
    public async Task AbrirCrediario_ComJuros_DeveAplicarCorreticao()
    {
        // 1000 / 3 parcelas a 2% ao mês — valor parcela deve ser maior que 333,33
        var resultado = await AbrirCrediario(total: 1000m, parcelas: 3, juros: 2m);

        resultado.ValorParcela.Should().BeGreaterThan(333.33m);
    }

    [Fact]
    public async Task PagarParcela_DeveAlterarStatusParaPago()
    {
        var crediario = await AbrirCrediario(total: 600m, parcelas: 3, juros: 0m);

        var crediarioDB = await _db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == crediario.Id);

        var primeiraParcela = crediarioDB!.Parcelas.OrderBy(p => p.Numero).First();
        var valorParcela = primeiraParcela.Valor;

        var pagResult = await _mediator.Send(new PagarParcelaCommand(primeiraParcela.Id, valorParcela));

        pagResult.Liquidado.Should().BeFalse(); // ainda tem 2 parcelas
        pagResult.SaldoRestante.Should().BeGreaterThan(0);

        await _db.Entry(primeiraParcela).ReloadAsync();
        primeiraParcela.Status.Should().Be(StatusParcela.Paga);
        primeiraParcela.ValorPago.Should().Be(valorParcela);
    }

    [Fact]
    public async Task PagarTodasParcelas_DeveLiquidarCrediario()
    {
        var crediario = await AbrirCrediario(total: 300m, parcelas: 3, juros: 0m);

        var crediarioDB = await _db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == crediario.Id);

        var parcelas = crediarioDB!.Parcelas.OrderBy(p => p.Numero).ToList();

        PagarParcelaResult? ultimo = null;
        foreach (var parcela in parcelas)
            ultimo = await _mediator.Send(new PagarParcelaCommand(parcela.Id, parcela.Valor));

        ultimo!.Liquidado.Should().BeTrue();
        ultimo.SaldoRestante.Should().Be(0m);

        await _db.Entry(crediarioDB).ReloadAsync();
        crediarioDB.Status.Should().Be(StatusCrediario.Liquidado);
    }

    [Fact]
    public async Task PagarParcela_ComValorDiferente_DevePersistirValorPago()
    {
        // Pagar com valor diferente do nominal (ex.: cliente pagou adiantado com desconto)
        var crediario = await AbrirCrediario(total: 500m, parcelas: 5, juros: 0m);

        var crediarioDB = await _db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == crediario.Id);

        var parcela = crediarioDB!.Parcelas.OrderBy(p => p.Numero).First();
        var valorPago = parcela.Valor + 5m; // pagamento com acréscimo

        await _mediator.Send(new PagarParcelaCommand(parcela.Id, valorPago));

        await _db.Entry(parcela).ReloadAsync();
        parcela.Status.Should().Be(StatusParcela.Paga);
        parcela.ValorPago.Should().Be(valorPago);
    }

    [Fact]
    public async Task AbrirCrediario_NumerosSequenciais_DevemSerDiferentes()
    {
        var c1 = await AbrirCrediario(total: 100m, parcelas: 1);
        var c2 = await AbrirCrediario(total: 200m, parcelas: 2);

        c1.Numero.Should().NotBe(c2.Numero);
    }

    [Fact]
    public async Task AbrirCrediario_EntradaMaiorQueTotal_DeveLancarExcecao()
    {
        var act = () => _mediator.Send(new AbrirCrediarioCommand(
            EmpresaId, ClienteId, UsuarioId,
            ValorTotal: 500m, ValorEntrada: 600m, // entrada > total
            NumeroParcelas: 3, TaxaJurosMensal: 0m));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task AbrirCrediario_ParcelasAcima60_DeveLancarExcecao()
    {
        var act = () => _mediator.Send(new AbrirCrediarioCommand(
            EmpresaId, ClienteId, UsuarioId,
            ValorTotal: 1000m, ValorEntrada: 0m,
            NumeroParcelas: 61, TaxaJurosMensal: 0m));

        await act.Should().ThrowAsync<Exception>();
    }

    public void Dispose() => _scope.Dispose();
}
