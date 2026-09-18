using Hangfire.Dashboard;

namespace Sistema.API.Auth;

/// <summary>
/// Autorização do dashboard do Hangfire (/jobs): só permite acesso LOCAL (loopback e sem
/// X-Forwarded-For). Por trás do nginx, requisições externas chegam com X-Forwarded-For, então
/// ficam bloqueadas — o painel só é acessível diretamente no servidor (ex.: via túnel SSH).
/// Evita expor histórico/argumentos de jobs e o disparo manual de tarefas na internet.
/// </summary>
public sealed class HangfireLocalDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http.Request.Headers.ContainsKey("X-Forwarded-For")) return false;
        var ip = http.Connection.RemoteIpAddress;
        return ip is not null && System.Net.IPAddress.IsLoopback(ip);
    }
}
