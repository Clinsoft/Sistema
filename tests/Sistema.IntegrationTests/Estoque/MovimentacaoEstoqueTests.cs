using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sistema.Application.Estoque.Commands;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Infrastructure.Data;
using Sistema.IntegrationTests.Infrastructure;

namespace Sistema.IntegrationTests.Estoque;

public class MovimentacaoEstoqueTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IMediator _mediator;
    private readonly SistemaDbContext _db;

    private static readonly Guid EmpresaId = Guid.NewGuid();
    private static readonly Guid LocalEstoqueId = Guid.NewGuid();
    private static readonly Guid CategoriaId = Guid.NewGuid();
    private static readonly Guid MarcaId = Guid.NewGuid();
    private static readonly Guid UnidadeId = Guid.NewGuid();

    public MovimentacaoEstoqueTests()
    {
        var provider = TestServiceProvider.Criar();
        _scope = provider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        _db = _scope.ServiceProvider.GetRequiredService<SistemaDbContext>();
    }

    private Task<Guid> CriarProduto(string codigo = "MOV-001") =>
        _mediator.Send(new CriarProdutoCommand(
            EmpresaId, codigo, "Produto Teste",
            CategoriaId, MarcaId, UnidadeId,
            CustoUnitario: 10m, PrecoVenda: 25m));

    [Fact]
    public async Task Entrada_DeveAumentarEstoqueDoProduto()
    {
        var produtoId = await CriarProduto();

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Entrada", 50, 10m));

        var produto = await _db.Produtos.FindAsync(produtoId);
        produto!.EstoqueAtual.Should().Be(50m);
    }

    [Fact]
    public async Task Saida_DeveReduzirEstoqueDoProduto()
    {
        var produtoId = await CriarProduto("MOV-002");

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Entrada", 100, 10m));

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Saida", 30, 10m));

        var produto = await _db.Produtos.FindAsync(produtoId);
        produto!.EstoqueAtual.Should().Be(70m);
    }

    [Fact]
    public async Task Entrada_DeveCriarRegistroDeMovimentacao()
    {
        var produtoId = await CriarProduto("MOV-003");

        var movId = await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Entrada", 25, 10m,
            DocumentoOrigem: "NF-123"));

        var mov = await _db.MovimentacoesEstoque.FindAsync(movId);
        mov.Should().NotBeNull();
        mov!.Quantidade.Should().Be(25m);
        mov.DocumentoOrigem.Should().Be("NF-123");
        mov.Tipo.Should().Be(TipoMovimentacao.Entrada);
    }

    [Fact]
    public async Task AjustePositivo_DeveAumentarEstoque()
    {
        var produtoId = await CriarProduto("MOV-004");

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Entrada", 10, 10m));

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "AjustePositivo", 5, 10m,
            Observacao: "Inventário físico"));

        var produto = await _db.Produtos.FindAsync(produtoId);
        produto!.EstoqueAtual.Should().Be(15m);
    }

    [Fact]
    public async Task AjusteNegativo_DeveReduzirEstoque()
    {
        var produtoId = await CriarProduto("MOV-005");

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Entrada", 20, 10m));

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "AjusteNegativo", 3, 10m,
            Observacao: "Produto avariado"));

        var produto = await _db.Produtos.FindAsync(produtoId);
        produto!.EstoqueAtual.Should().Be(17m);
    }

    [Fact]
    public async Task Devolucao_DeveAumentarEstoque()
    {
        var produtoId = await CriarProduto("MOV-006");

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Entrada", 50, 10m));
        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Saida", 10, 10m));
        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Devolucao", 2, 10m));

        var produto = await _db.Produtos.FindAsync(produtoId);
        produto!.EstoqueAtual.Should().Be(42m); // 50 - 10 + 2
    }

    [Fact]
    public async Task RegistrarMovimentacao_TipoInvalido_DeveLancarExcecao()
    {
        var produtoId = await CriarProduto("MOV-007");

        var act = () => _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "TipoInexistente", 10, 10m));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task RegistrarMovimentacao_ProdutoInexistente_DeveLancarExcecao()
    {
        var act = () => _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, Guid.NewGuid(), LocalEstoqueId, "Entrada", 10, 10m));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MultiplasMov_DeveAcumularCorreto()
    {
        var produtoId = await CriarProduto("MOV-008");

        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Entrada", 100, 10m));
        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Saida", 25, 10m));
        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Saida", 15, 10m));
        await _mediator.Send(new RegistrarMovimentacaoCommand(
            EmpresaId, produtoId, LocalEstoqueId, "Entrada", 50, 9m));

        var produto = await _db.Produtos.FindAsync(produtoId);
        produto!.EstoqueAtual.Should().Be(110m); // 100 - 25 - 15 + 50

        var totalMovs = await _db.MovimentacoesEstoque
            .CountAsync(m => m.ProdutoId == produtoId);
        totalMovs.Should().Be(4);
    }

    public void Dispose() => _scope.Dispose();
}
