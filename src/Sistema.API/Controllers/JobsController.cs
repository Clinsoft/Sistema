using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sistema.API.Controllers;

/// <summary>
/// Saúde dos jobs automáticos (Hangfire): última execução, estado e falhas.
/// Serve para não deixar um job quebrar em silêncio (ex.: disparo de WhatsApp).
/// </summary>
[ApiController]
[Route("api/jobs")]
[Authorize(Roles = "Administrador")]
public class JobsController : ControllerBase
{
    [HttpGet("saude")]
    public IActionResult Saude()
    {
        var mon = JobStorage.Current.GetMonitoringApi();
        var stats = mon.GetStatistics();

        var recorrentes = JobStorage.Current.GetConnection().GetRecurringJobs()
            .OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.Cron,
                ultimaExecucao = r.LastExecution,
                proximaExecucao = r.NextExecution,
                ultimoEstado = r.LastJobState,   // "Succeeded" | "Failed" | "Processing" | null
                erro = r.Error,
            })
            .ToList();

        var falhasRecentes = mon.FailedJobs(0, 15)
            .Select(f => new
            {
                job = f.Value.Job?.Method?.Name ?? "?",
                erro = f.Value.ExceptionMessage,
                falhouEm = f.Value.FailedAt,
            })
            .ToList();

        return Ok(new
        {
            estatisticas = new
            {
                sucesso = stats.Succeeded,
                falha = stats.Failed,
                processando = stats.Processing,
                agendados = stats.Scheduled,
                naFila = stats.Enqueued,
                recorrentes = stats.Recurring,
            },
            recorrentes,
            falhasRecentes,
        });
    }
}
