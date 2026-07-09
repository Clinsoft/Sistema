using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sistema.Application.Estoque.Commands;
using Sistema.Application.Vendas.Commands;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;
using Sistema.IntegrationTests.Infrastructure;

namespace Sistema.IntegrationTests.Vendas;

/// <summary>
/// Testes de integração: use cases de Venda usando banco InMemory.
/// Cada teste cria um ServiceProvider próprio para isolamento total.
/// </summary>
public class RealizarVendaTests
{
    private static readonly Guid CategoriaId = Guid.NewGuid();
    private static readonly Guid MarcaId = Guid.NewGuid();
    private static readonly Guid UnidadeId = Guid.NewGuid();

    // Cria um provider isolado por chamada
    private static (IServiceProvider Provider, IServiceScope Scope, IMediator Mediator, SistemaDbContext Db)
        CriarContexto()
    {
        var provider = TestServiceProvider.Criar();
        var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<SistemaDbContext>();
        return (provider, scope, mediator, db);
    }

    private static Produto CriarProdutoNoBanco(SistemaDbContext db,
        Guid empresaId, string codigo = "P001", decimal preco = 50m)
    {
        var produto = Produto.Criar(empresaId, codigo, "Produto Teste",
            CategoriaId, MarcaId, UnidadeId, 20m, preco);
        produto.AjustarEstoque(100);
        db.Produtos.Add(produto);
        db.SaveChanges();
        return produto;
    }

    private static Guid IniciarVenda(SistemaDbContext db, Guid empresaId)
    {
        var numero = (db.Vendas.Count(v => v.EmpresaId == empresaId) + 1).ToString("D6");
        var venda = Venda.Iniciar(empresaId, Guid.NewGuid(), Guid.NewGuid(), numero);
        db.Vendas.Add(venda);
        db.SaveChanges();
        return venda.Id;
    }

    private static void AdicionarItem(SistemaDbContext db, Guid vendaId,
        Guid produtoId, decimal qtd, decimal preco)
    {
        var venda = db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .First(v => v.Id == vendaId);
        var countAntes = venda.Itens.Count;
        venda.AdicionarItem(produtoId, "Produto Teste", qtd, preco);
        // Rastreia explicitamente os novos filhos (coleções com backing field readonly)
        foreach (var item in venda.Itens.Skip(countAntes))
            db.ItensVenda.Add(item);
        db.SaveChanges();
    }

    private static void AdicionarPagamento(SistemaDbContext db, Guid vendaId,
        FormaPagamento forma, decimal valor)
    {
        var venda = db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .First(v => v.Id == vendaId);
        var countAntes = venda.Pagamentos.Count;
        venda.AdicionarPagamento(forma, valor);
        foreach (var pag in venda.Pagamentos.Skip(countAntes))
            db.PagamentosVenda.Add(pag);
        db.SaveChanges();
    }

    [Fact]
    public void FluxoCompleto_IniciarAdicionarFinalizar_DevePersistirVendaFinalizada()
    {
        var (_, scope, _, db) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        var produto = CriarProdutoNoBanco(db, empresaId);
        var vendaId = IniciarVenda(db, empresaId);

        AdicionarItem(db, vendaId, produto.Id, 3, 50m); // total = 150
        AdicionarPagamento(db, vendaId, FormaPagamento.Pix, 150m);

        var venda = db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .First(v => v.Id == vendaId);
        venda.Finalizar();
        db.SaveChanges();

        var vendaDb = db.Vendas.Find(vendaId)!;
        vendaDb.Status.Should().Be(StatusVenda.Finalizada);
        vendaDb.Total.Should().Be(150m);
    }

    [Fact]
    public async Task Finalizar_DeveBaixarEstoque_NoLocalDaVenda()
    {
        var (provider, scope, mediator, db) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        var localEstoqueId = Guid.NewGuid();
        var produto = CriarProdutoNoBanco(db, empresaId); // estoque inicial 100

        var numero = (db.Vendas.Count(v => v.EmpresaId == empresaId) + 1).ToString("D6");
        var venda = Venda.Iniciar(empresaId, Guid.NewGuid(), localEstoqueId, numero);
        db.Vendas.Add(venda);
        db.SaveChanges();

        AdicionarItem(db, venda.Id, produto.Id, 4, 50m);
        AdicionarPagamento(db, venda.Id, FormaPagamento.Pix, 200m);

        var vendaCompleta = db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .First(v => v.Id == venda.Id);
        vendaCompleta.Finalizar();

        // SaveChangesAsync despacha o VendaFinalizadaEvent → handler baixa o estoque
        await db.SaveChangesAsync();

        // A movimentação de saída deve usar o LocalEstoqueId da venda (não Guid.Empty)
        var mov = db.MovimentacoesEstoque.Single(m => m.ProdutoId == produto.Id);
        mov.LocalEstoqueId.Should().Be(localEstoqueId);
        mov.LocalEstoqueId.Should().NotBe(Guid.Empty);
        mov.Tipo.Should().Be(TipoMovimentacao.Saida);
        mov.Quantidade.Should().Be(4m);

        db.Produtos.Find(produto.Id)!.EstoqueAtual.Should().Be(96m); // 100 - 4
    }

    [Fact]
    public void Venda_ComTroco_DeveCalcularTrocoCorretamente()
    {
        var (_, scope, _, db) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        var produto = CriarProdutoNoBanco(db, empresaId, "P002", 30m);
        var vendaId = IniciarVenda(db, empresaId);

        AdicionarItem(db, vendaId, produto.Id, 2, 30m); // total = 60
        AdicionarPagamento(db, vendaId, FormaPagamento.Dinheiro, 100m);

        var venda = db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .First(v => v.Id == vendaId);
        venda.Finalizar();
        db.SaveChanges();

        db.Entry(venda).Reload();
        venda.Troco.Should().Be(40m);
        venda.Total.Should().Be(60m);
    }

    [Fact]
    public void Venda_PagamentoParcial_DeveLancarExcecao()
    {
        var (_, scope, _, db) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        var produto = CriarProdutoNoBanco(db, empresaId, "P003", 100m);
        var vendaId = IniciarVenda(db, empresaId);

        AdicionarItem(db, vendaId, produto.Id, 1, 100m);
        AdicionarPagamento(db, vendaId, FormaPagamento.Dinheiro, 50m);

        var venda = db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .First(v => v.Id == vendaId);

        var act = () => venda.Finalizar();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Pagamento*");
    }

    [Fact]
    public void Venda_PagamentoMultiplosMeios_DeveSomarCorretamente()
    {
        var (_, scope, _, db) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        var produto = CriarProdutoNoBanco(db, empresaId, "P004", 80m);
        var vendaId = IniciarVenda(db, empresaId);

        AdicionarItem(db, vendaId, produto.Id, 1, 80m);
        AdicionarPagamento(db, vendaId, FormaPagamento.Dinheiro, 50m);
        AdicionarPagamento(db, vendaId, FormaPagamento.Pix, 30m);

        var venda = db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .First(v => v.Id == vendaId);
        venda.Finalizar();
        db.SaveChanges();

        db.Entry(venda).Reload();
        venda.TotalPago.Should().Be(80m);
        venda.Troco.Should().Be(0m);
    }

    [Fact]
    public void Venda_SemItens_DeveLancarExcecaoAoFinalizar()
    {
        var (_, scope, _, db) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        var vendaId = IniciarVenda(db, empresaId);
        AdicionarPagamento(db, vendaId, FormaPagamento.Pix, 0m);

        var venda = db.Vendas
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .First(v => v.Id == vendaId);

        var act = () => venda.Finalizar();
        act.Should().Throw<InvalidOperationException>().WithMessage("*itens*");
    }

    [Fact]
    public void Venda_Cancelar_DeveAlterarStatus()
    {
        var (_, scope, _, db) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        var vendaId = IniciarVenda(db, empresaId);

        var venda = db.Vendas.First(v => v.Id == vendaId);
        venda.Cancelar("desistência do cliente");
        db.SaveChanges();

        db.Entry(venda).Reload();
        venda.Status.Should().Be(StatusVenda.Cancelada);
        venda.Observacao.Should().Contain("desistência");
    }

    [Fact]
    public async Task CriarProduto_Command_SemCodigo_GeraCodigoAutomatico()
    {
        var (_, scope, mediator, db) = CriarContexto();
        using var _ = scope;

        // Código vazio → o backend gera automaticamente um código livre.
        var id = await mediator.Send(new CriarProdutoCommand(
            Guid.NewGuid(), "", "Produto",
            CategoriaId, MarcaId, UnidadeId, 10m, 25m));

        id.Should().NotBeEmpty();
        var produto = await db.Produtos.FindAsync(id);
        produto!.Codigo.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CriarProduto_Command_DeveValidarPrecoPositivo()
    {
        var (_, scope, mediator, _) = CriarContexto();
        using var _ = scope;

        var act = async () => await mediator.Send(new CriarProdutoCommand(
            Guid.NewGuid(), "SKU-X", "Produto",
            CategoriaId, MarcaId, UnidadeId, 10m, -5m)); // preco negativo

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CriarProduto_CodigoDuplicado_DeveLancarExcecao()
    {
        var (_, scope, mediator, _) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        await mediator.Send(new CriarProdutoCommand(
            empresaId, "DUP-001", "Produto A",
            CategoriaId, MarcaId, UnidadeId, 10m, 25m));

        var act = async () => await mediator.Send(new CriarProdutoCommand(
            empresaId, "DUP-001", "Produto B", // mesmo código e empresa
            CategoriaId, MarcaId, UnidadeId, 15m, 30m));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DUP-001*");
    }

    [Fact]
    public async Task RegistrarMovimentacao_EntradaSaida_DeveAcumularEstoqueCorreto()
    {
        var (_, scope, mediator, db) = CriarContexto();
        using var _ = scope;

        var empresaId = Guid.NewGuid();
        var localId = Guid.NewGuid();
        var produtoId = await mediator.Send(new CriarProdutoCommand(
            empresaId, "MOV-INT", "Produto Movimentado",
            CategoriaId, MarcaId, UnidadeId, 10m, 25m));

        await mediator.Send(new RegistrarMovimentacaoCommand(
            empresaId, produtoId, localId, "Entrada", 50m, 10m));
        await mediator.Send(new RegistrarMovimentacaoCommand(
            empresaId, produtoId, localId, "Saida", 15m, 10m));

        var produto = await db.Produtos.FindAsync(produtoId);
        produto!.EstoqueAtual.Should().Be(35m);
    }
}
