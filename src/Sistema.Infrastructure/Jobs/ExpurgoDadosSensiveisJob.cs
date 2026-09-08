using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>LGPD — expurgo de dado biométrico: apaga a FOTO de rosto dos aceites
/// após o período de retenção, mantendo as demais evidências (assinatura,
/// geolocalização, IP, data/hora e hash), que continuam com valor probatório.</summary>
public class ExpurgoDadosSensiveisJob(SistemaDbContext db, ILogger<ExpurgoDadosSensiveisJob> logger)
{
    // Retenção da foto (dado biométrico sensível). Ajustável conforme a política da empresa.
    public const int RetencaoFotoMeses = 24;

    public async Task ExecutarAsync()
    {
        var limite = DateTime.UtcNow.AddMonths(-RetencaoFotoMeses);
        var afetados = await db.AceitesTermoPremiacao
            .Where(a => a.FotoBase64 != null && a.DataAceite < limite)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.FotoBase64, (string?)null));
        if (afetados > 0)
            logger.LogInformation("[LGPD] Expurgo de {N} foto(s) de aceite com mais de {M} meses.", afetados, RetencaoFotoMeses);
    }
}
