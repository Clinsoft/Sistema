using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Jobs;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>Previsão de folha de pagamento (salários + encargos FGTS/INSS).</summary>
[ApiController]
[Route("api/folha")]
[Authorize]
public class FolhaController(SistemaDbContext db, ILogger<FolhaPagamentoJob> logger) : ControllerBase
{
    /// <summary>
    /// Gera manualmente a previsão da folha da competência informada (ou do mês atual).
    /// Idempotente: se a competência já foi gerada, não duplica.
    /// </summary>
    [HttpPost("gerar-previsao")]
    public async Task<IActionResult> Gerar([FromQuery] int? ano, [FromQuery] int? mes)
    {
        var referencia = ano.HasValue && mes.HasValue
            ? new DateTime(ano.Value, mes.Value, 1)
            : DateTime.Today;

        var qtd = await new FolhaPagamentoJob(db, logger).GerarFolhaAsync(referencia);
        return Ok(new { competencia = referencia.ToString("yyyy-MM"), contasGeradas = qtd });
    }
}
