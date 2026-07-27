using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Jobs;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>Geração das mensalidades fixas de fornecedores (contador, aluguel, etc.).</summary>
[ApiController]
[Route("api/despesas-fixas")]
[Authorize]
public class DespesasFixasController(SistemaDbContext db, ILogger<DespesasFixasJob> logger) : ControllerBase
{
    /// <summary>
    /// Gera as contas a pagar de mensalidade da competência atual (ou informada).
    /// Idempotente: não duplica fornecedor já gerado no mês.
    /// </summary>
    [HttpPost("gerar")]
    public async Task<IActionResult> Gerar([FromQuery] int? ano, [FromQuery] int? mes)
    {
        var referencia = ano.HasValue && mes.HasValue
            ? new DateTime(ano.Value, mes.Value, 1)
            : DateTime.Today;

        var qtd = await new DespesasFixasJob(db, logger).GerarAsync(referencia);
        return Ok(new { competencia = referencia.ToString("yyyy-MM"), contasGeradas = qtd });
    }
}
