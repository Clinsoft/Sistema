using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Primitives;
using Sistema.Domain.Vendas.Entities;
using System.Reflection;

namespace Sistema.Infrastructure.Data;

public class SistemaDbContext(DbContextOptions<SistemaDbContext> options) : DbContext(options)
{
    // Cadastros
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();

    // Estoque
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<UnidadeMedida> UnidadesMedida => Set<UnidadeMedida>();
    public DbSet<LocalEstoque> LocaisEstoque => Set<LocalEstoque>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();

    // Vendas
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemVenda> ItensVenda => Set<ItemVenda>();
    public DbSet<PagamentoVenda> PagamentosVenda => Set<PagamentoVenda>();
    public DbSet<PDVSessao> PDVSessoes => Set<PDVSessao>();

    // Compras
    public DbSet<PedidoCompra> PedidosCompra => Set<PedidoCompra>();
    public DbSet<ItemPedidoCompra> ItensPedidoCompra => Set<ItemPedidoCompra>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.GetType().GetProperty("AtualizadoEm")
                    ?.SetValue(entry.Entity, DateTime.UtcNow);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
