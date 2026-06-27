using Hangfire;
using Sistema.API.Extensions;
using Sistema.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Clinsoft API", Version = "v1" });
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

app.MapControllers();

app.Run();
