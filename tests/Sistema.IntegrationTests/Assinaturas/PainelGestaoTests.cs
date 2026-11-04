using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sistema.API.Controllers;
using Sistema.Domain.Assinaturas;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.IntegrationTests.Assinaturas;

/// <summary>Entrada do painel de gestão: só com a chave certa, e cada ação com a sua trava.</summary>
public class PainelGestaoTests
{
    private const string Chave = "chave-de-teste-com-mais-de-32-caracteres";

    private static SistemaDbContext Banco() => new(new DbContextOptionsBuilder<SistemaDbContext>()
        .UseInMemoryDatabase($"Painel_{Guid.NewGuid()}").Options);

    private static PainelGestaoController Painel(SistemaDbContext db, string? configurada = Chave, string? enviada = Chave)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PainelGestao:Chave"] = configurada }).Build();
        var contexto = new DefaultHttpContext();
        if (enviada is not null) contexto.Request.Headers["X-Painel-Chave"] = enviada;
        return new PainelGestaoController(new GestaoAssinaturas(db, config, new MemoryCache(new MemoryCacheOptions())), config, NullLogger<PainelGestaoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = contexto },
        };
    }

    private static async Task<Assinatura> Cliente(SistemaDbContext db)
    {
        var empresa = Empresa.Criar("Loja Teste", "Loja Teste", "11222333000181", "SN", "Rua", "1", "Centro", "Cidade", "SP", "01001000", "11999999999", "loja@teste.local");
        var assinatura = Assinatura.CriarTrial(empresa.Id, PlanoAssinatura.Essencial);
        db.Add(empresa);
        db.Add(assinatura);
        await db.SaveChangesAsync();
        return assinatura;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curta")]
    public async Task SemChaveBoaConfigurada_FicaFechado(string? configurada)
    {
        using var db = Banco();

        var r = await Painel(db, configurada, configurada).Clientes(default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("errada")]
    public async Task ChaveErrada_ERecusadaESemMudarNada(string? enviada)
    {
        using var db = Banco();
        var a = await Cliente(db);

        (await Painel(db, enviada: enviada).Clientes(default)).Should().BeOfType<UnauthorizedResult>();
        (await Painel(db, enviada: enviada).Bloquear(a.EmpresaId, new MotivoDto("x"), default)).Should().BeOfType<UnauthorizedResult>();

        a.Status.Should().Be(StatusAssinatura.Trial);
    }

    [Fact]
    public async Task ListaOsClientes()
    {
        using var db = Banco();
        var a = await Cliente(db);

        var r = await Painel(db).Clientes(default);

        var corpo = r.Should().BeOfType<OkObjectResult>().Subject.Value!;
        var lista = (List<ClienteAssinatura>)corpo.GetType().GetProperty("clientes")!.GetValue(corpo)!;
        lista.Should().ContainSingle(c => c.EmpresaId == a.EmpresaId && c.Nome == "Loja Teste" && c.Situacao == "TrialAtivo");
    }

    [Fact]
    public async Task Bloquear_ExigeMotivo()
    {
        using var db = Banco();
        var a = await Cliente(db);

        (await Painel(db).Bloquear(a.EmpresaId, new MotivoDto("  "), default)).Should().BeOfType<BadRequestObjectResult>();
        a.Status.Should().Be(StatusAssinatura.Trial);

        (await Painel(db).Bloquear(a.EmpresaId, new MotivoDto("Pedido do cliente"), default)).Should().BeOfType<OkObjectResult>();
        a.Status.Should().Be(StatusAssinatura.Bloqueada);
        a.Observacao.Should().Be("Pedido do cliente");
    }

    [Fact]
    public async Task Reativar_TiraOBloqueio()
    {
        using var db = Banco();
        var a = await Cliente(db);
        a.Bloquear("x");
        await db.SaveChangesAsync();

        (await Painel(db).Reativar(a.EmpresaId, default)).Should().BeOfType<OkObjectResult>();

        a.Status.Should().Be(StatusAssinatura.Ativa);
    }

    [Theory]
    [InlineData(0)]    // hoje
    [InlineData(-5)]   // passado
    public async Task Pagar_RecusaVencimentoQueNaoEFuturo(int dias)
    {
        using var db = Banco();
        var a = await Cliente(db);

        var r = await Painel(db).RegistrarPagamento(a.EmpresaId, new PagarDto(DateTime.Today.AddDays(dias)), default);

        r.Should().BeOfType<BadRequestObjectResult>();
        a.ProximoVencimento.Should().BeNull();
    }

    [Fact]
    public async Task Pagar_SemVencimentoERecusado_EComVencimentoLibera()
    {
        using var db = Banco();
        var a = await Cliente(db);

        (await Painel(db).RegistrarPagamento(a.EmpresaId, new PagarDto(), default)).Should().BeOfType<BadRequestObjectResult>();

        var venc = DateTime.Today.AddDays(30);
        (await Painel(db).RegistrarPagamento(a.EmpresaId, new PagarDto(venc), default)).Should().BeOfType<OkObjectResult>();

        a.Status.Should().Be(StatusAssinatura.Ativa);
        a.ProximoVencimento.Should().Be(venc);
    }

    [Fact]
    public async Task Estender_SoQuemEstaEmTeste()
    {
        using var db = Banco();
        var a = await Cliente(db);
        a.RegistrarPagamento(DateTime.Today.AddDays(30));
        await db.SaveChangesAsync();

        (await Painel(db).EstenderTrial(a.EmpresaId, new DiasDto(7), default)).Should().BeOfType<BadRequestObjectResult>();

        a.Status.Should().Be(StatusAssinatura.Ativa);   // quem paga não volta para teste grátis
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task Estender_RecusaDiasForaDoLimite(int dias)
    {
        using var db = Banco();
        var a = await Cliente(db);
        var antes = a.TrialAte;

        (await Painel(db).EstenderTrial(a.EmpresaId, new DiasDto(dias), default)).Should().BeOfType<BadRequestObjectResult>();

        a.TrialAte.Should().Be(antes);
    }

    [Fact]
    public async Task Estender_TesteJaVencido_ContaAPartirDeHoje()
    {
        using var db = Banco();
        var a = await Cliente(db);
        a.EstenderTrial(-100);   // venceu há muito tempo
        await db.SaveChangesAsync();
        a.Situacao().Should().Be(SituacaoAssinatura.TrialExpirado);

        (await Painel(db).EstenderTrial(a.EmpresaId, new DiasDto(7), default)).Should().BeOfType<OkObjectResult>();

        a.Situacao().Should().Be(SituacaoAssinatura.TrialAtivo);
        a.TrialAte.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task EmpresaSemAssinatura_Da404()
    {
        using var db = Banco();

        (await Painel(db).Reativar(Guid.NewGuid(), default)).Should().BeOfType<NotFoundObjectResult>();
    }
}
