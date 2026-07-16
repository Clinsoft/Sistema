using MediatR;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Crediario.Entities;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Contabilidade.Entities;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Marketing.Entities;
using Sistema.Domain.Shared.Primitives;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.WhatsApp.Entities;
using System.Reflection;

namespace Sistema.Infrastructure.Data;

public class SistemaDbContext(DbContextOptions<SistemaDbContext> options, IMediator? mediator = null) : DbContext(options)
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
    public DbSet<ProdutoEmbalagem> ProdutosEmbalagem => Set<ProdutoEmbalagem>();
    public DbSet<AlimentoTaco> AlimentosTaco => Set<AlimentoTaco>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<TabelaNutricional> TabelasNutricionais => Set<TabelaNutricional>();
    public DbSet<ReceitaProduto> ReceitasProduto => Set<ReceitaProduto>();
    public DbSet<SugestaoProduto> SugestoesProduto => Set<SugestaoProduto>();
    public DbSet<QrCodeProduto> QrCodesProduto => Set<QrCodeProduto>();

    // Vendas
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemVenda> ItensVenda => Set<ItemVenda>();
    public DbSet<PagamentoVenda> PagamentosVenda => Set<PagamentoVenda>();
    public DbSet<PDVSessao> PDVSessoes => Set<PDVSessao>();
    public DbSet<OperacaoCaixa> OperacoesCaixa => Set<OperacaoCaixa>();
    public DbSet<MetaVendaMensal> MetasVendaMensal => Set<MetaVendaMensal>();
    public DbSet<DevolucaoVenda> DevolucoesVenda => Set<DevolucaoVenda>();
    public DbSet<ItemDevolucao> ItensDevolucoesVenda => Set<ItemDevolucao>();

    // Compras
    public DbSet<PedidoCompra> PedidosCompra => Set<PedidoCompra>();
    public DbSet<ItemPedidoCompra> ItensPedidoCompra => Set<ItemPedidoCompra>();

    // Crediário
    public DbSet<Crediario> Crediarios => Set<Crediario>();
    public DbSet<ParcelaCrediario> ParcelasCrediario => Set<ParcelaCrediario>();
    public DbSet<RenegociacaoCrediario> RenegociacoesCrediario => Set<RenegociacaoCrediario>();

    // WhatsApp
    public DbSet<CatalogoWhatsApp> CatalogosWhatsApp => Set<CatalogoWhatsApp>();
    public DbSet<ItemCatalogo> ItensCatalogo => Set<ItemCatalogo>();
    public DbSet<PedidoWhatsApp> PedidosWhatsApp => Set<PedidoWhatsApp>();
    public DbSet<ItemPedidoWhatsApp> ItensPedidoWhatsApp => Set<ItemPedidoWhatsApp>();

    // Financeiro
    public DbSet<CategoriaFinanceira> CategoriasFinanceiras => Set<CategoriaFinanceira>();
    public DbSet<ContaBancaria> ContasBancarias => Set<ContaBancaria>();
    public DbSet<LancamentoFinanceiro> LancamentosFinanceiros => Set<LancamentoFinanceiro>();
    public DbSet<MovimentacaoBancaria> MovimentacoesBancarias => Set<MovimentacaoBancaria>();
    public DbSet<CustoFixo> CustosFixos => Set<CustoFixo>();
    public DbSet<OperadoraCartao> OperadorasCartao => Set<OperadoraCartao>();
    public DbSet<RecebivelCartao> ReceiveisCartao => Set<RecebivelCartao>();

    // Contabilidade
    public DbSet<ContaContabil> PlanoContas => Set<ContaContabil>();
    public DbSet<LancamentoContabil> LancamentosContabeis => Set<LancamentoContabil>();
    public DbSet<PartidaContabil> PartidasContabeis => Set<PartidaContabil>();
    public DbSet<Contador> Contadores => Set<Contador>();

    // Fiscal
    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();
    public DbSet<ItemNotaFiscal> ItensNotaFiscal => Set<ItemNotaFiscal>();
    public DbSet<ConfiguracaoFiscal> ConfiguracoesFiscais => Set<ConfiguracaoFiscal>();
    public DbSet<NotaFiscalRecebida> NotasFiscaisRecebidas => Set<NotaFiscalRecebida>();
    public DbSet<EntradaNFe> EntradasNFe => Set<EntradaNFe>();
    public DbSet<ItemEntradaNFe> ItensEntradaNFe => Set<ItemEntradaNFe>();

    // Marketing
    public DbSet<TemplateMarketing> TemplatesMarketing => Set<TemplateMarketing>();
    public DbSet<ArteMarketing> ArtesMarketing => Set<ArteMarketing>();
    public DbSet<AgendamentoPublicacao> AgendamentosPublicacao => Set<AgendamentoPublicacao>();
    public DbSet<Promocao> Promocoes => Set<Promocao>();
    public DbSet<MembroClube> MembrosClube => Set<MembroClube>();
    public DbSet<MovimentoCashback> MovimentosCashback => Set<MovimentoCashback>();
    public DbSet<ConfiguracaoClube> ConfiguracoesClube => Set<ConfiguracaoClube>();
    public DbSet<ConfiguracaoValidade> ConfiguracoesValidade => Set<ConfiguracaoValidade>();
    public DbSet<ConfiguracaoEtiqueta> ConfiguracoesEtiqueta => Set<ConfiguracaoEtiqueta>();
    public DbSet<AlertaValidade> AlertasValidade => Set<AlertaValidade>();
    public DbSet<ConfiguracaoWhatsAppMensagem> ConfiguracoesWhatsAppMensagem => Set<ConfiguracaoWhatsAppMensagem>();
    public DbSet<TemplateWhatsAppMensagem> TemplatesWhatsAppMensagem => Set<TemplateWhatsAppMensagem>();
    public DbSet<HistoricoMensagemWhatsApp> HistoricosMensagensWhatsApp => Set<HistoricoMensagemWhatsApp>();

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

        // Coleta domain events antes de salvar
        var entitiesWithEvents = ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Despacha domain events após persistência
        if (mediator is not null)
        {
            foreach (var entity in entitiesWithEvents)
            {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();
                foreach (var domainEvent in events)
                    await mediator.Publish(domainEvent, cancellationToken);
            }
        }

        return result;
    }
}
