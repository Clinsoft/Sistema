using FluentValidation;
using System.Text.Json;

namespace Sistema.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException ex)
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "application/json";
            var erros = ex.Errors.Select(e => new { campo = e.PropertyName, mensagem = e.ErrorMessage });
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { tipo = "validacao", erros }));
        }
        catch (KeyNotFoundException ex)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { mensagem = ex.Message }));
        }
        catch (InvalidOperationException ex)
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { mensagem = ex.Message }));
        }
        catch (UnauthorizedAccessException ex)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { mensagem = ex.Message }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado: {Message}", ex.Message);
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            // Detalhe real (inclusive da exceção interna, ex.: truncamento SQL) para diagnóstico.
            var detalhe = ex.InnerException?.Message ?? ex.Message;
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                mensagem = "Erro interno do servidor.",
                detalhe
            }));
        }
    }
}
