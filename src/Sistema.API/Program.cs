using Hangfire;
using Sistema.API.Extensions;
using Sistema.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "EcoGranel API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var key = System.Text.Encoding.UTF8.GetBytes(
            builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret não configurado."));
        opt.TokenValidationParameters = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// Blindagem do perfil "Contador": acesso total só às áreas fiscal/contábil;
// leitura (GET) no restante; escrita e módulos sensíveis (folha, financeiro,
// vendas, usuários) bloqueados. Reforça as travas de tela/rota do frontend.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (context.User.Identity?.IsAuthenticated == true
        && context.User.IsInRole("Contador")
        && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
    {
        static bool Comeca(string p, string prefixo) => p.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase);

        // Área do contador (acesso total) e autenticação (sempre liberada).
        var areaContador = Comeca(path, "/api/contabilidade") || Comeca(path, "/api/auth");

        var metodo = context.Request.Method;
        var escrita = HttpMethods.IsPost(metodo) || HttpMethods.IsPut(metodo)
                   || HttpMethods.IsPatch(metodo) || HttpMethods.IsDelete(metodo);

        // Módulos sensíveis: bloqueados até para leitura.
        string[] sensiveis =
        [
            "/api/usuarios", "/api/folha", "/api/das", "/api/despesas-fixas",
            "/api/financeiro", "/api/contas-pagar", "/api/contas-receber", "/api/crediario",
            "/api/relatorios", "/api/vendas", "/api/caixa", "/api/sessoes",
            "/api/marketing", "/api/whatsapp", "/api/fiscal"
        ];
        var sensivel = sensiveis.Any(s => Comeca(path, s));

        if (!areaContador && (sensivel || escrita))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { mensagem = "Acesso restrito para o perfil Contador." });
            return;
        }
    }
    await next();
});

app.UseHangfireDashboard("/jobs");

// Registrar jobs recorrentes
RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.EstoqueAlertaJob>(
    "estoque-alerta-minimo",
    job => job.ExecutarAsync(),
    "0 8 * * *");   // 08:00 todo dia

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.CrediarioLembreteJob>(
    "crediario-lembrete-parcelas",
    job => job.ExecutarAsync(),
    "0 9 * * *");   // 09:00 todo dia

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.FinanceiroAlertaJob>(
    "financeiro-alerta-vencimentos",
    job => job.ExecutarAsync(),
    "0 8 * * *");   // 08:00 todo dia

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.RecebivelCartaoBaixaJob>(
    "recebivel-cartao-baixa-automatica",
    job => job.ExecutarAsync(),
    "0 7 * * *");   // 07:00 todo dia — marca Recebido quando o crédito cai (D+prazo)
// Roda uma vez ao subir para regularizar os recebíveis já vencidos.
BackgroundJob.Enqueue<Sistema.Infrastructure.Jobs.RecebivelCartaoBaixaJob>(
    job => job.ExecutarAsync());

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.TaxaCartaoDespesaJob>(
    "taxa-cartao-despesa-variavel",
    job => job.ExecutarAsync(),
    "0 23 * * *");  // 23:00 todo dia — soma a taxa do dia e lança como despesa variável
// Roda uma vez ao subir para gerar as despesas dos dias já passados.
BackgroundJob.Enqueue<Sistema.Infrastructure.Jobs.TaxaCartaoDespesaJob>(
    job => job.ExecutarAsync());

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.BackupJob>(
    "backup-banco-dados",
    job => job.ExecutarAsync(),
    "0 2 * * *");   // 02:00 toda madrugada

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.ValidadeJob>(
    "validade-monitoramento",
    job => job.ExecutarAsync(),
    "0 8 * * *");   // 08:00 todo dia — verifica vencimentos e gera promoções

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.WhatsAppDisparoJob>(
    "whatsapp-disparos-automaticos",
    job => job.ExecutarAsync(),
    "0 8 * * *");   // 08:00 todo dia — aniversariantes, promoções, novidades

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.LimparVendasAbertasJob>(
    "limpar-vendas-abertas",
    job => job.ExecutarAsync(),
    "0 * * * *");   // de hora em hora — descarta vendas em aberto há +6h

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.FolhaPagamentoJob>(
    "folha-previsao-mensal",
    job => job.ExecutarAsync(),
    "0 6 1 * *");   // 06:00 do dia 1º — previsão de salários + FGTS/INSS da folha

RecurringJob.AddOrUpdate<Sistema.Infrastructure.Jobs.DespesasFixasJob>(
    "despesas-fixas-mensais",
    job => job.ExecutarAsync(),
    "10 6 1 * *");  // 06:10 do dia 1º — mensalidades fixas (contador, aluguel, etc.)

app.MapControllers();

app.Run();
