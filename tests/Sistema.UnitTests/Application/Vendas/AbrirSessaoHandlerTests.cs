using System.Linq.Expressions;
using FluentAssertions;
using Sistema.Application.Vendas.Commands;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.UnitTests.Application.Vendas;

/// <summary>
/// Proteção de caixa: ao abrir um caixa, se o operador já tem uma sessão aberta
/// (deixada aberta), o handler fecha automaticamente a antiga com o saldo esperado
/// e abre a nova — em vez de bloquear.
/// </summary>
public class AbrirSessaoHandlerTests
{
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly Guid Loja = Guid.NewGuid();

    [Fact]
    public async Task Abrir_SemSessaoAberta_CriaNovaSessao()
    {
        var repo = new FakeSessaoRepo();
        var vendas = new FakeVendaRepo(dinheiro: 0, troco: 0);
        var uow = new FakeUoW();
        var handler = new AbrirSessaoHandler(repo, vendas, uow);

        var id = await handler.Handle(new AbrirSessaoCommand(Empresa, Usuario, Loja, 100m), default);

        repo.Sessoes.Should().HaveCount(1);
        repo.Sessoes[0].Id.Should().Be(id);
        repo.Sessoes[0].Status.Should().Be(StatusSessao.Aberta);
        uow.Salvou.Should().BeTrue();
    }

    [Fact]
    public async Task Abrir_ComSessaoDeixadaAberta_FechaAntigaEabreNova()
    {
        // Caixa esquecido aberto (fundo 150, sem vendas).
        var antiga = PDVSessao.Abrir(Empresa, Usuario, Loja, 150m);
        var repo = new FakeSessaoRepo(antiga);
        var vendas = new FakeVendaRepo(dinheiro: 0, troco: 0);
        var uow = new FakeUoW();
        var handler = new AbrirSessaoHandler(repo, vendas, uow);

        var novoId = await handler.Handle(new AbrirSessaoCommand(Empresa, Usuario, Loja, 0m), default);

        // A antiga foi fechada automaticamente com o saldo esperado (= fundo, pois não houve venda).
        antiga.Status.Should().Be(StatusSessao.Fechada);
        antiga.SaldoFechamento.Should().Be(150m);
        antiga.ObservacaoFechamento.Should().Contain("Fechamento automático");

        // E uma nova sessão foi aberta.
        var nova = repo.Sessoes.Single(s => s.Id == novoId);
        nova.Status.Should().Be(StatusSessao.Aberta);
        repo.Sessoes.Count(s => s.Status == StatusSessao.Aberta).Should().Be(1);
    }

    [Fact]
    public async Task Abrir_ComSessaoAbertaComDinheiro_FechaComSaldoEsperado()
    {
        var antiga = PDVSessao.Abrir(Empresa, Usuario, Loja, 100m);
        var repo = new FakeSessaoRepo(antiga);
        // Recebeu 250 em dinheiro e devolveu 30 de troco → líquido 220.
        var vendas = new FakeVendaRepo(dinheiro: 250m, troco: 30m);
        var handler = new AbrirSessaoHandler(repo, vendas, new FakeUoW());

        await handler.Handle(new AbrirSessaoCommand(Empresa, Usuario, Loja, 0m), default);

        // Esperado = fundo 100 + (250 - 30) = 320.
        antiga.SaldoFechamento.Should().Be(320m);
    }

    // ── Fakes em memória (sem libs de mock) ──────────────────────────────────
    private sealed class FakeUoW : IUnitOfWork
    {
        public bool Salvou { get; private set; }
        public Task<int> SalvarAsync(CancellationToken ct = default) { Salvou = true; return Task.FromResult(1); }
    }

    private sealed class FakeVendaRepo(decimal dinheiro, decimal troco) : IVendaRepository
    {
        public Task<(decimal Dinheiro, decimal Troco)> TotaisDinheiroAsync(Guid empresaId, DateTime inicio, DateTime fim,
            Guid? usuarioId = null, Guid? localEstoqueId = null, CancellationToken ct = default)
            => Task.FromResult((dinheiro, troco));

        public Task<decimal> TotalVendidasAsync(Guid empresaId, DateTime inicio, DateTime fim,
            Guid? usuarioId = null, Guid? localEstoqueId = null, CancellationToken ct = default) => Task.FromResult(0m);
        public Task<Venda?> ObterComItensAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Venda?>(null);
        public Task<IReadOnlyList<Venda>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Venda>>(new List<Venda>());
        public Task<string> ProximoNumeroAsync(Guid empresaId, CancellationToken ct = default) => Task.FromResult("1");
        public Task<Venda?> ObterPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Venda?>(null);
        public Task<IReadOnlyList<Venda>> ListarAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Venda>>(new List<Venda>());
        public Task<IReadOnlyList<Venda>> ListarAsync(Expression<Func<Venda, bool>> p, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Venda>>(new List<Venda>());
        public Task AdicionarAsync(Venda entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Atualizar(Venda entity) { }
        public void Remover(Venda entity) { }
        public Task<bool> ExisteAsync(Expression<Func<Venda, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> ContarAsync(Expression<Func<Venda, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeSessaoRepo : IPDVSessaoRepository
    {
        public List<PDVSessao> Sessoes { get; } = new();
        public FakeSessaoRepo(params PDVSessao[] iniciais) => Sessoes.AddRange(iniciais);

        public Task<PDVSessao?> ObterSessaoAbertaAsync(Guid empresaId, Guid usuarioId, CancellationToken ct = default)
            => Task.FromResult(Sessoes.FirstOrDefault(s =>
                s.EmpresaId == empresaId && s.UsuarioId == usuarioId && s.Status == StatusSessao.Aberta));

        public Task<IReadOnlyList<PDVSessao>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PDVSessao>>(Sessoes);

        public Task AdicionarAsync(PDVSessao entity, CancellationToken ct = default) { Sessoes.Add(entity); return Task.CompletedTask; }
        public void Atualizar(PDVSessao entity) { if (!Sessoes.Contains(entity)) Sessoes.Add(entity); }
        public Task<PDVSessao?> ObterPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Sessoes.FirstOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<PDVSessao>> ListarAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PDVSessao>>(Sessoes);
        public Task<IReadOnlyList<PDVSessao>> ListarAsync(Expression<Func<PDVSessao, bool>> p, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PDVSessao>>(Sessoes);
        public void Remover(PDVSessao entity) => Sessoes.Remove(entity);
        public Task<bool> ExisteAsync(Expression<Func<PDVSessao, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> ContarAsync(Expression<Func<PDVSessao, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
    }
}
