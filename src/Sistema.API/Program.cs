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
