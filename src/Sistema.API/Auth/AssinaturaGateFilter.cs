using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sistema.Domain.Assinaturas;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Auth;

/// <summary>
/// Gate de assinatura (a trava REAL do SaaS, no servidor):
///  • se a assinatura da empresa não está em dia (bloqueada/expirada/vencida além da
///    tolerância) ⇒ 402 em todos os endpoints de negócio (exceto os marcados
///    <see cref="PermitirBloqueadoAttribute"/> e os anônimos);
///  • se a action/controller exige um recurso via <see cref="RequerRecursoAttribute"/> e o
///    plano não cobre ⇒ 402.
/// Empresa SEM assinatura (instalações antigas / EcoGranel) passa livre.
/// </summary>
public sealed class AssinaturaGateFilter(SistemaDbContext db, IMemoryCache cache) : IAsyncActionFilter
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var user = ctx.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(user.FindFirst("empresaId")?.Value, out var empresaId)
            || empresaId == Guid.Empty)
        {
            await next();
            return;
        }

        var assinatura = await ObterAsync(empresaId, ctx.HttpContext.RequestAborted);
        if (assinatura is null) { await next(); return; }   // sem assinatura = acesso total

        var agora = DateTime.UtcNow;
        var cad = ctx.ActionDescriptor as ControllerActionDescriptor;

        // 1) Status: bloqueada/expirada trava tudo, menos endpoints marcados como permitidos.
        if (!assinatura.PodeUsar(agora) && !Tem<PermitirBloqueadoAttribute>(cad))
        {
            ctx.Result = new ObjectResult(new
            {
                mensagem = "Assinatura inativa. Regularize para continuar usando o sistema.",
                situacao = assinatura.Situacao(agora).ToString(),
                bloqueada = true,
            })
            { StatusCode = StatusCodes.Status402PaymentRequired };
            return;
        }

        // 2) Recurso do plano.
        var req = Obter<RequerRecursoAttribute>(cad);
        if (req is not null && !assinatura.TemRecurso(req.Recurso, agora))
        {
            ctx.Result = new ObjectResult(new
            {
                mensagem = "Este recurso não está incluído no seu plano. Faça upgrade para desbloquear.",
                recurso = req.Recurso.ToString(),
                plano = assinatura.Plano.ToString(),
                upgrade = true,
            })
            { StatusCode = StatusCodes.Status402PaymentRequired };
            return;
        }

        await next();
    }

    /// <summary>Snapshot da assinatura (cache curto para não bater no banco a cada request).</summary>
    private async Task<Assinatura?> ObterAsync(Guid empresaId, CancellationToken ct)
    {
        var chave = $"assinatura:{empresaId}";
        if (cache.TryGetValue(chave, out var cached))
            return cached as Assinatura;

        var a = await db.Assinaturas.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct);
        cache.Set(chave, a, Ttl);   // guarda inclusive o null (empresa sem assinatura)
        return a;
    }

    private static T? Obter<T>(ControllerActionDescriptor? cad) where T : Attribute
        => cad?.MethodInfo.GetCustomAttribute<T>() ?? cad?.ControllerTypeInfo.GetCustomAttribute<T>();

    private static bool Tem<T>(ControllerActionDescriptor? cad) where T : Attribute
        => Obter<T>(cad) is not null;
}
