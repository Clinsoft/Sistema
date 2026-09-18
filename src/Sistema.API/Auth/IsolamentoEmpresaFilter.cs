using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sistema.API.Auth;

/// <summary>
/// Isolamento multi-empresa (defesa contra IDOR entre tenants): em toda requisição
/// AUTENTICADA, se algum argumento carregar um <c>empresaId</c> (parâmetro de query/rota
/// chamado "empresaId" OU propriedade "EmpresaId" no corpo), ele DEVE bater com o
/// <c>empresaId</c> do token. Diferente ⇒ 403. Assim um usuário da empresa A não consegue
/// ler/gravar dados da empresa B trocando o parâmetro. Endpoints anônimos passam direto.
/// </summary>
public sealed class IsolamentoEmpresaFilter : IAsyncActionFilter
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> _cacheProp = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var user = ctx.HttpContext.User;
        if (user?.Identity?.IsAuthenticated == true
            && Guid.TryParse(user.FindFirst("empresaId")?.Value, out var empresaToken)
            && empresaToken != Guid.Empty)
        {
            foreach (var (nome, valor) in ctx.ActionArguments)
            {
                if (valor is null) continue;
                Guid? doPedido = null;

                if (valor is Guid g && string.Equals(nome, "empresaId", StringComparison.OrdinalIgnoreCase))
                    doPedido = g;
                else if (valor is not string && valor.GetType().IsClass)
                {
                    var prop = _cacheProp.GetOrAdd(valor.GetType(),
                        t => t.GetProperty("EmpresaId", BindingFlags.Public | BindingFlags.Instance));
                    if (prop?.GetValue(valor) is Guid pg) doPedido = pg;
                }

                if (doPedido.HasValue && doPedido.Value != Guid.Empty && doPedido.Value != empresaToken)
                {
                    ctx.Result = new ObjectResult(new { mensagem = "Acesso negado: empresa inválida para este usuário." })
                    { StatusCode = StatusCodes.Status403Forbidden };
                    return;
                }
            }
        }

        await next();
    }
}
