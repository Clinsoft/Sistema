using FluentValidation;
using Hangfire;
using Hangfire.SqlServer;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Sistema.Application.Common.Behaviors;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Interfaces;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Repositories;
using Sistema.Infrastructure.Repositories.Cadastros;
using Sistema.Infrastructure.Repositories.Estoque;
using Sistema.Infrastructure.Repositories.Vendas;
using Sistema.Domain.Crediario.Interfaces;
using Sistema.Infrastructure.Repositories.Crediario;
using Sistema.Domain.Compras.Interfaces;
using Sistema.Infrastructure.Repositories.Compras;
using Sistema.Domain.Auth;
using Sistema.Infrastructure.Auth;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Infrastructure.Repositories.Financeiro;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Infrastructure.Repositories.Fiscal;
using Sistema.Infrastructure.Fiscal;

namespace Sistema.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var appAssembly = typeof(ValidationBehavior<,>).Assembly;
        var infraAssembly = typeof(Sistema.Infrastructure.Data.SistemaDbContext).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(appAssembly);
            cfg.RegisterServicesFromAssembly(infraAssembly);
        });
        services.AddValidatorsFromAssembly(appAssembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

        services.AddDbContext<SistemaDbContext>(opt =>
            opt.UseSqlServer(connectionString, sql =>
            {
                // 1205 = deadlock. Não vem na lista padrão do EF, então precisa ser
                // adicionado para a estratégia de retry repetir a transação vítima.
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(3),
                    errorNumbersToAdd: new[] { 1205 });
                sql.CommandTimeout(60);
            }));

        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer();

        QuestPDF.Settings.License = LicenseType.Community;

        // Repositórios
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IFornecedorRepository, FornecedorRepository>();
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IVendaRepository, VendaRepository>();
        services.AddScoped<IDevolucaoVendaRepository, DevolucaoVendaRepository>();
        services.AddScoped<ICrediarioRepository, CrediarioRepository>();
        services.AddScoped<IParcelaCrediarioRepository, ParcelaCrediarioRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IMovimentacaoEstoqueRepository, MovimentacaoEstoqueRepository>();
        services.AddScoped<IPedidoCompraRepository, PedidoCompraRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPDVSessaoRepository, PDVSessaoRepository>();
        services.AddScoped<ILoteRepository, LoteRepository>();
        services.AddScoped<IQrCodeProdutoRepository, QrCodeProdutoRepository>();
        services.AddScoped<ITabelaNutricionalRepository, TabelaNutricionalRepository>();
        services.AddScoped<IReceitaProdutoRepository, ReceitaProdutoRepository>();
        services.AddScoped<ISugestaoProdutoRepository, SugestaoProdutoRepository>();

        // Financeiro
        services.AddScoped<ILancamentoFinanceiroRepository, LancamentoFinanceiroRepository>();
        services.AddScoped<IContaBancariaRepository, ContaBancariaRepository>();

        // E-mail
        services.AddScoped<IEmailService, Sistema.Infrastructure.Email.SmtpEmailService>();

        // Fiscal
        services.AddScoped<INotaFiscalRepository, NotaFiscalRepository>();
        services.AddScoped<IConfiguracaoFiscalRepository, ConfiguracaoFiscalRepository>();
        services.AddScoped<Sistema.Infrastructure.Fiscal.SpedFiscalService>();
        services.AddScoped<Sistema.Domain.Fiscal.Interfaces.IDanfeService,
            Sistema.Infrastructure.Fiscal.DanfeService>();
        services.AddScoped<IDistribuicaoDFeService, DistribuicaoDFeService>();
        services.AddScoped<INFeTransmissaoService, NFeTransmissaoService>();

        // WhatsApp Cloud API
        services.AddHttpClient<Sistema.Infrastructure.Services.WhatsAppCloudApiService>();

        // Gemini (Nano Banana 2) — geração de imagens para artes de marketing
        services.AddHttpClient<Sistema.Infrastructure.Services.GeminiImageService>();

        // OpenAI (ChatGPT) — geração de texto (ex.: descrição complementar de produto)
        services.AddHttpClient<Sistema.Infrastructure.Services.OpenAiTextService>();
        // OpenAI (gpt-image-1) — geração de imagens para artes de marketing
        services.AddHttpClient<Sistema.Infrastructure.Services.OpenAiImageService>(c =>
            c.Timeout = TimeSpan.FromMinutes(3));
        // Branding — sobrepõe a logo oficial nas artes geradas por IA
        services.AddScoped<Sistema.Infrastructure.Services.ArteBrandingService>();
        // Busca de imagem de produto por código de barras (Cosmos + Open Food Facts)
        services.AddHttpClient<Sistema.Infrastructure.Services.ImagemProdutoService>(c =>
            c.Timeout = TimeSpan.FromSeconds(20));
        // Busca de imagem por semelhança do nome (granel/KG) — Openverse (licença livre)
        services.AddHttpClient<Sistema.Infrastructure.Services.ImagemPorNomeService>(c =>
            c.Timeout = TimeSpan.FromSeconds(25));
        // Sincronização de produtos com o site público (ecogranel.com.br)
        services.AddHttpClient<Sistema.Infrastructure.Services.SiteSyncService>(c =>
            c.Timeout = TimeSpan.FromSeconds(60));
        services.AddScoped<Sistema.Infrastructure.Jobs.WhatsAppDisparoJob>();
        services.AddScoped<Sistema.Infrastructure.Services.WhatsAppIaAtendenteService>();
        services.AddScoped<Sistema.Infrastructure.Jobs.RecebivelCartaoBaixaJob>();
        services.AddScoped<Sistema.Infrastructure.Jobs.TaxaCartaoDespesaJob>();
        services.AddScoped<Sistema.Infrastructure.Jobs.PreencherImagensProdutoJob>();

        return services;
    }
}
