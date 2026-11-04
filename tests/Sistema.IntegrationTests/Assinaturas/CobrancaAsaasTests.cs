using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
using Sistema.Infrastructure.Services;

namespace Sistema.IntegrationTests.Assinaturas;

/// <summary>Conta única do Asaas: troca de plano sem cobrança em dobro; aviso só com token; estorno desfaz o período pago.</summary>
public class CobrancaAsaasTests
{
    private const string Token = "token-de-teste";

    private sealed class AsaasFalso : HttpMessageHandler
    {
        public List<string> Chamadas { get; } = [];
        public HttpStatusCode RespostaCancelar { get; set; } = HttpStatusCode.OK;
        public bool FalharCriarAssinatura { get; set; }
        public Queue<HttpStatusCode> RespostasMudarValor { get; } = new();
        public JsonElement? AssinaturaCriada { get; private set; }
        public JsonElement? CobrancaMudada { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var caminho = r.RequestUri!.AbsolutePath;
            Chamadas.Add($"{r.Method} {caminho}");
            var enviado = r.Content is null ? null : await r.Content.ReadAsStringAsync(ct);
            if (r.Method == HttpMethod.Post && caminho.EndsWith("/subscriptions")) AssinaturaCriada = JsonSerializer.Deserialize<JsonElement>(enviado!);
            var mudar = r.Method == HttpMethod.Put && RespostasMudarValor.TryDequeue(out var fila) ? fila : HttpStatusCode.OK;
            if (r.Method == HttpMethod.Put && mudar == HttpStatusCode.OK) CobrancaMudada = JsonSerializer.Deserialize<JsonElement>(enviado!);
            var (status, corpo) = (r.Method.Method, caminho) switch
            {
                ("DELETE", _) => (RespostaCancelar, "{}"),
                ("PUT", _) => (mudar, "{}"),
                ("POST", var c) when c.EndsWith("/subscriptions") => FalharCriarAssinatura
                    ? (HttpStatusCode.BadRequest, "{}") : (HttpStatusCode.OK, """{"id":"sub_nova"}"""),
                ("POST", _) => (HttpStatusCode.OK, """{"id":"cus_novo"}"""),
                _ => (HttpStatusCode.OK, """{"data":[{"id":"pay_1","value":229,"invoiceUrl":"https://asaas.test/fatura"}]}"""),
            };
            return new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }

    private static SistemaDbContext Banco() => new(new DbContextOptionsBuilder<SistemaDbContext>()
        .UseInMemoryDatabase($"Asaas_{Guid.NewGuid()}").Options);

    private static IConfiguration Config(string? webhookToken = Token) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Asaas:ApiKey"] = "chave-de-teste",
            ["Asaas:BaseUrl"] = "https://asaas.test/v3",
            ["Asaas:WebhookToken"] = webhookToken,
        }).Build();

    private static async Task<(Guid EmpresaId, Assinatura Assinatura)> EmpresaAssinante(SistemaDbContext db)
    {
        var empresa = Empresa.Criar("Loja Teste", "Loja Teste", "11222333000181", "SN", "Rua", "1", "Centro", "Cidade", "SP", "01001000", "11999999999", "loja@teste.local");
        var assinatura = Assinatura.CriarTrial(empresa.Id, PlanoAssinatura.Essencial);
        assinatura.VincularAsaas("cus_antigo", "sub_antiga");
        db.Add(empresa);
        db.Add(assinatura);
        await db.SaveChangesAsync();
        return (empresa.Id, assinatura);
    }

    private static AssinaturasController Checkout(SistemaDbContext db, AsaasFalso asaas, Guid empresaId) =>
        new(db, new AsaasService(new HttpClient(asaas), Config()), new MemoryCache(new MemoryCacheOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("empresaId", empresaId.ToString())])) },
            },
        };

    private static WebhooksController Webhook(SistemaDbContext db, IConfiguration config, string? token = Token)
    {
        var contexto = new DefaultHttpContext();
        if (token is not null) contexto.Request.Headers["asaas-access-token"] = token;
        return new WebhooksController(db, config, new MemoryCache(new MemoryCacheOptions()), NullLogger<WebhooksController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = contexto },
        };
    }

    private static JsonElement Aviso(string evento, string assinatura, string vencimento) => JsonSerializer.Deserialize<JsonElement>(
        JsonSerializer.Serialize(new { @event = evento, payment = new { subscription = assinatura, dueDate = vencimento } }));

    [Fact]
    public async Task TrocarPlano_CancelaAAntigaAntesDeCriarANova()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        var asaas = new AsaasFalso();

        var r = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        r.Should().BeOfType<OkObjectResult>();
        var cancelou = asaas.Chamadas.IndexOf("DELETE /v3/subscriptions/sub_antiga");
        var criou = asaas.Chamadas.IndexOf("POST /v3/subscriptions");
        cancelou.Should().BeGreaterOrEqualTo(0);
        criou.Should().BeGreaterThan(cancelou);
        assinatura.AsaasSubscriptionId.Should().Be("sub_nova");
        assinatura.Plano.Should().Be(PlanoAssinatura.Profissional);
    }

    [Fact]
    public async Task TrocarPlano_SeCancelarFalhar_NaoCriaOutraEMantemTudo()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        var asaas = new AsaasFalso { RespostaCancelar = HttpStatusCode.InternalServerError };

        var r = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);
        asaas.Chamadas.Should().NotContain("POST /v3/subscriptions");
        assinatura.AsaasSubscriptionId.Should().Be("sub_antiga");
        assinatura.Plano.Should().Be(PlanoAssinatura.Essencial);
    }

    [Fact]
    public async Task TrocarPlano_SeCriarFalhar_NaoGuardaAAntigaCancelada_EPodeTentarDeNovo()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        var asaas = new AsaasFalso { FalharCriarAssinatura = true };

        var r = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);
        assinatura.AsaasSubscriptionId.Should().BeNull();

        asaas.FalharCriarAssinatura = false;
        var deNovo = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        deNovo.Should().BeOfType<OkObjectResult>();
        assinatura.AsaasSubscriptionId.Should().Be("sub_nova");
        asaas.Chamadas.Count(c => c.StartsWith("DELETE")).Should().Be(1);
    }

    [Fact]
    public async Task TrocarPlano_EmDia_MantemOVencimentoESomaADiferencaSoNaProximaCobranca()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        var pagoAte = DateTime.Today.AddDays(10);
        assinatura.RegistrarPagamento(pagoAte);
        var diferenca = assinatura.DiferencaProporcional(PlanoAssinatura.Profissional, DateTime.Today);
        var asaas = new AsaasFalso();

        var r = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        r.Should().BeOfType<OkObjectResult>();
        diferenca.Should().BeGreaterThan(0);
        asaas.AssinaturaCriada!.Value.GetProperty("value").GetDecimal().Should().Be(229m);   // a recorrência fica com o valor normal
        asaas.AssinaturaCriada!.Value.GetProperty("nextDueDate").GetString().Should().Be(pagoAte.ToString("yyyy-MM-dd"));
        asaas.CobrancaMudada!.Value.GetProperty("value").GetDecimal().Should().Be(229m + diferenca);
        asaas.Chamadas.Should().Contain("PUT /v3/payments/pay_1");
        assinatura.Plano.Should().Be(PlanoAssinatura.Profissional);
        assinatura.ProximoVencimento.Should().Be(pagoAte);   // os dias pagos continuam valendo
    }

    [Fact]
    public async Task DescerDePlano_EmDia_NaoCobraAgoraNemGeraCredito()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        var pagoAte = DateTime.Today.AddDays(10);
        assinatura.RegistrarPagamento(pagoAte);
        var asaas = new AsaasFalso();

        await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Micro"), default);

        asaas.AssinaturaCriada!.Value.GetProperty("value").GetDecimal().Should().Be(79m);
        asaas.AssinaturaCriada!.Value.GetProperty("nextDueDate").GetString().Should().Be(pagoAte.ToString("yyyy-MM-dd"));
        asaas.Chamadas.Should().NotContain(c => c.StartsWith("PUT"));
    }

    [Fact]
    public async Task TrocarPlano_SemEstarEmDia_CobraOValorCheioHoje()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        assinatura.RegistrarPagamento(DateTime.Today.AddDays(-3));
        assinatura.EstenderTrial(-100);   // teste grátis já acabou
        assinatura.Reativar();
        var asaas = new AsaasFalso();

        await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        asaas.AssinaturaCriada!.Value.GetProperty("value").GetDecimal().Should().Be(229m);
        asaas.AssinaturaCriada!.Value.GetProperty("nextDueDate").GetString().Should().Be(DateTime.Today.ToString("yyyy-MM-dd"));
        asaas.Chamadas.Should().NotContain(c => c.StartsWith("PUT"));
    }

    [Fact]
    public async Task SoMudarOCiclo_EmDia_ComecaNoVencimentoSemDiferenca()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        var pagoAte = DateTime.Today.AddDays(10);
        assinatura.RegistrarPagamento(pagoAte);
        var asaas = new AsaasFalso();

        await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Essencial", "Anual"), default);

        asaas.AssinaturaCriada!.Value.GetProperty("value").GetDecimal().Should().Be(1190m);
        asaas.AssinaturaCriada!.Value.GetProperty("nextDueDate").GetString().Should().Be(pagoAte.ToString("yyyy-MM-dd"));
        asaas.Chamadas.Should().NotContain(c => c.StartsWith("PUT"));
    }

    [Fact]
    public async Task MudarValor_ComErroPassageiro_TentaDeNovoEFunciona()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        assinatura.RegistrarPagamento(DateTime.Today.AddDays(10));
        var asaas = new AsaasFalso();
        asaas.RespostasMudarValor.Enqueue(HttpStatusCode.InternalServerError);

        var r = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        r.Should().BeOfType<OkObjectResult>();
        asaas.Chamadas.Count(c => c.StartsWith("PUT")).Should().Be(2);
        assinatura.Plano.Should().Be(PlanoAssinatura.Profissional);
    }

    [Fact]
    public async Task MudarValor_SempreComErro_DesfazATrocaEPodeTentarDeNovo()
    {
        using var db = Banco();
        var (empresaId, assinatura) = await EmpresaAssinante(db);
        var pagoAte = DateTime.Today.AddDays(10);
        assinatura.RegistrarPagamento(pagoAte);
        var asaas = new AsaasFalso();
        for (var i = 0; i < 3; i++) asaas.RespostasMudarValor.Enqueue(HttpStatusCode.InternalServerError);

        var r = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);
        asaas.Chamadas.Should().Contain("DELETE /v3/subscriptions/sub_nova");
        assinatura.AsaasSubscriptionId.Should().BeNull();
        assinatura.Plano.Should().Be(PlanoAssinatura.Essencial);
        assinatura.ProximoVencimento.Should().Be(pagoAte);

        var deNovo = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Profissional"), default);

        deNovo.Should().BeOfType<OkObjectResult>();
        assinatura.AsaasSubscriptionId.Should().Be("sub_nova");
        assinatura.Plano.Should().Be(PlanoAssinatura.Profissional);
    }

    [Fact]
    public async Task MesmoPlano_DevolveOLink_SemCancelarNemCriar()
    {
        using var db = Banco();
        var (empresaId, _) = await EmpresaAssinante(db);
        var asaas = new AsaasFalso();

        var r = await Checkout(db, asaas, empresaId).Checkout(new CheckoutDto("Essencial"), default);

        r.Should().BeOfType<OkObjectResult>();
        asaas.Chamadas.Should().OnlyContain(c => c.StartsWith("GET"));
    }

    [Fact]
    public async Task Cancelar_AssinaturaQueJaNaoExiste_NaoEErro()
    {
        var asaas = new AsaasFalso { RespostaCancelar = HttpStatusCode.NotFound };
        var servico = new AsaasService(new HttpClient(asaas), Config());

        await servico.Invoking(s => s.CancelarAssinaturaAsync("sub_sumiu")).Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null, 503)]   // sem token configurado: recusa
    [InlineData("", 503)]
    public async Task Aviso_SemTokenConfigurado_ERecusado(string? configurado, int esperado)
    {
        using var db = Banco();
        var (_, assinatura) = await EmpresaAssinante(db);

        var r = await Webhook(db, Config(configurado)).Asaas(Aviso("PAYMENT_RECEIVED", "sub_antiga", "2026-11-10"), default);

        r.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(esperado);
        assinatura.ProximoVencimento.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("errado")]
    public async Task Aviso_ComTokenErrado_NaoMarcaComoPago(string? recebido)
    {
        using var db = Banco();
        var (_, assinatura) = await EmpresaAssinante(db);

        var r = await Webhook(db, Config(), recebido).Asaas(Aviso("PAYMENT_RECEIVED", "sub_antiga", "2026-11-10"), default);

        r.Should().BeOfType<UnauthorizedResult>();
        assinatura.ProximoVencimento.Should().BeNull();
    }

    [Fact]
    public async Task Aviso_DeOutroSistemaNaMesmaConta_EIgnorado()
    {
        using var db = Banco();
        var (_, assinatura) = await EmpresaAssinante(db);

        var r = await Webhook(db, Config()).Asaas(Aviso("PAYMENT_RECEIVED", "sub_do_atenda_mais", "2026-11-10"), default);

        r.Should().BeOfType<OkObjectResult>();
        assinatura.ProximoVencimento.Should().BeNull();
    }

    [Theory]
    [InlineData("PAYMENT_REFUNDED")]
    [InlineData("PAYMENT_CHARGEBACK_REQUESTED")]
    public async Task Estorno_E_Contestacao_DesfazemOPeriodoPago(string evento)
    {
        using var db = Banco();
        var (_, assinatura) = await EmpresaAssinante(db);
        await Webhook(db, Config()).Asaas(Aviso("PAYMENT_RECEIVED", "sub_antiga", "2026-11-10"), default);
        assinatura.ProximoVencimento.Should().Be(new DateTime(2026, 12, 10));

        await Webhook(db, Config()).Asaas(Aviso(evento, "sub_antiga", "2026-11-10"), default);

        assinatura.ProximoVencimento.Should().Be(new DateTime(2026, 11, 10));
    }

    [Fact]
    public async Task EstornoParcial_NaoMudaOAcesso()
    {
        using var db = Banco();
        var (_, assinatura) = await EmpresaAssinante(db);
        await Webhook(db, Config()).Asaas(Aviso("PAYMENT_RECEIVED", "sub_antiga", "2026-11-10"), default);

        await Webhook(db, Config()).Asaas(Aviso("PAYMENT_PARTIALLY_REFUNDED", "sub_antiga", "2026-11-10"), default);

        assinatura.ProximoVencimento.Should().Be(new DateTime(2026, 12, 10));
    }
}
