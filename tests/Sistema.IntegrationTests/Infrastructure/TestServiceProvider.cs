using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Sistema.Application.Common.Behaviors;
using Sistema.Application.Crediario.Commands;
using Sistema.Application.Estoque.Commands;
using Sistema.Application.Vendas.Commands;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Crediario.Interfaces;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Interfaces;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Repositories;
using Sistema.Infrastructure.Repositories.Cadastros;
using Sistema.Infrastructure.Repositories.Crediario;
using Sistema.Infrastructure.Repositories.Estoque;
using Sistema.Infrastructure.Repositories.Financeiro;
using Sistema.Infrastructure.Repositories.Vendas;

namespace Sistema.IntegrationTests.Infrastructure;

public static class TestServiceProvider
{
    public static IServiceProvider Criar(string? nomeDb = null)
    {
        var services = new ServiceCollection();

        services.AddDbContext<SistemaDbContext>(opt =>
            opt.UseInMemoryDatabase(nomeDb ?? $"ClinSoft_{Guid.NewGuid()}"));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(IniciarVendaCommand).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(CriarProdutoCommand).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(AbrirCrediarioCommand).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Registra todos os validadores da camada Application
        services.AddValidatorsFromAssembly(typeof(IniciarVendaCommand).Assembly);

        services.AddScoped<IVendaRepository, VendaRepository>();
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IMovimentacaoEstoqueRepository, MovimentacaoEstoqueRepository>();
        services.AddScoped<ICrediarioRepository, CrediarioRepository>();
        services.AddScoped<IParcelaCrediarioRepository, ParcelaCrediarioRepository>();
        services.AddScoped<ILancamentoFinanceiroRepository, LancamentoFinanceiroRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services.BuildServiceProvider();
    }
}
