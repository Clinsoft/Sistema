using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Assinaturas;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Efetiva o BLOQUEIO automático das assinaturas: trial que venceu e assinaturas ativas
/// vencidas além da tolerância passam a Status=Bloqueada. O acesso já é barrado por data no
/// gate (Situacao), mas este job persiste o status (painel/relatórios ficam corretos e para
/// futura régua de cobrança). Roda algumas vezes ao dia.
/// </summary>
public class AssinaturaStatusJob(SistemaDbContext db, ILogger<AssinaturaStatusJob> logger)
{
    [AutomaticRetry(Attempts = 1)]
    public async Task ExecutarAsync()
    {
        var agora = DateTime.UtcNow;
        // Só as que ainda estão marcadas como Trial/Ativa podem virar Bloqueada.
        var candidatas = await db.Assinaturas
            .Where(a => a.Status == StatusAssinatura.Trial || a.Status == StatusAssinatura.Ativa)
            .ToListAsync();

        var bloqueadas = 0;
        foreach (var a in candidatas)
        {
            switch (a.Situacao(agora))
            {
                case SituacaoAssinatura.TrialExpirado:
                    a.Bloquear("Teste grátis expirado");
                    bloqueadas++;
                    break;
                case SituacaoAssinatura.Bloqueada:   // Ativa vencida além da tolerância
                    a.Bloquear("Inadimplência (vencida além da tolerância)");
                    bloqueadas++;
                    break;
            }
        }

        if (bloqueadas > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Assinaturas bloqueadas automaticamente: {N}", bloqueadas);
        }
    }
}
