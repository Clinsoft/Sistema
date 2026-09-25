using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sistema.Domain.Assinaturas;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers;

/// <summary>Webhooks de gateways de pagamento. Público (o gateway chama), validado por token.</summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public class WebhooksController(SistemaDbContext db, IConfiguration config,
    IMemoryCache cache, ILogger<WebhooksController> logger) : ControllerBase
{
    /// <summary>Recebe eventos do Asaas (pagamento confirmado/recebido/vencido) e atualiza a assinatura.</summary>
    [HttpPost("asaas")]
    public async Task<IActionResult> Asaas([FromBody] JsonElement body, CancellationToken ct)
    {
        // Validação: o Asaas envia o token configurado no header 'asaas-access-token'.
        var esperado = config["Asaas:WebhookToken"];
        if (!string.IsNullOrWhiteSpace(esperado))
        {
            var recebido = Request.Headers["asaas-access-token"].FirstOrDefault();
            if (!string.Equals(recebido, esperado, StringComparison.Ordinal))
                return Unauthorized();
        }

        var evento = body.TryGetProperty("event", out var ev) ? ev.GetString() : null;
        if (!body.TryGetProperty("payment", out var pay))
            return Ok(new { ignorado = true });

        var subId = pay.TryGetProperty("subscription", out var s) ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(subId)) return Ok(new { ignorado = "sem assinatura" });

        var assin = await db.Assinaturas.FirstOrDefaultAsync(a => a.AsaasSubscriptionId == subId, ct);
        if (assin is null) return Ok(new { ignorado = "assinatura desconhecida" });

        var venc = pay.TryGetProperty("dueDate", out var d) && DateTime.TryParse(d.GetString(), out var dv)
            ? dv : DateTime.Today;

        switch (evento)
        {
            case "PAYMENT_CONFIRMED":
            case "PAYMENT_RECEIVED":
                // Acesso válido até o próximo vencimento (data desta cobrança + 1 ciclo).
                var proximo = assin.Ciclo == CicloCobranca.Anual ? venc.AddYears(1) : venc.AddMonths(1);
                assin.RegistrarPagamento(proximo);
                await db.SaveChangesAsync(ct);
                cache.Remove($"assinatura:{assin.EmpresaId}");
                logger.LogInformation("Asaas {Evento}: assinatura {Emp} ativa até {Venc:d}", evento, assin.EmpresaId, proximo);
                break;

            // Vencido/estornado: a tolerância + o job de status cuidam do bloqueio; só registramos.
            case "PAYMENT_OVERDUE":
            case "PAYMENT_REFUNDED":
            case "PAYMENT_CHARGEBACK":
                logger.LogInformation("Asaas {Evento}: assinatura {Emp}", evento, assin.EmpresaId);
                break;
        }

        return Ok(new { ok = true });
    }
}
