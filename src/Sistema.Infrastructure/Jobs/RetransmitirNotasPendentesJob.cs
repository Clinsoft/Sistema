using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Rede de segurança: reenfileira a transmissão das NFC-e presas em 'Transmitindo'
/// (a SEFAZ ficou fora além da janela de retentativa do TransmitirNFCeJob, ou o job
/// se perdeu). Só pega notas com mais de 3h — depois do backoff automático — para
/// não concorrer com uma transmissão em andamento. Idempotente (o job de transmissão
/// ignora notas já resolvidas).
/// </summary>
public class RetransmitirNotasPendentesJob(
    SistemaDbContext db,
    IBackgroundJobClient jobs,
    ILogger<RetransmitirNotasPendentesJob> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecutarAsync()
    {
        var limite = DateTime.UtcNow.AddHours(-3);

        // (A) Notas presas em 'Transmitindo' há +3h (janela de retentativa automática já passou).
        var pendentes = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.Status == StatusNF.Transmitindo
                     && n.XmlEnvio != null && n.XmlEnvio != ""
                     && n.CriadoEm < limite)
            .OrderBy(n => n.CriadoEm)
            .Select(n => n.Id)
            .Take(300)
            .ToListAsync();

        // (B) Notas marcadas 'Rejeitada' por falha de COMUNICAÇÃO (SEFAZ fora/timeout) — não
        // são rejeições reais. Volta para 'Transmitindo' e reenfileira. NUNCA mexe em rejeições
        // de schema/dados/regra (essas precisam de correção humana).
        var recuperar = await db.NotasFiscais
            .Where(n => n.Status == StatusNF.Rejeitada
                     && n.XmlEnvio != null && n.XmlEnvio != "" && n.ChaveAcesso != null
                     && (n.MotivoRejeicao!.StartsWith("Erro de comunicação")
                         || n.MotivoRejeicao.StartsWith("Erro ao parsear resposta")
                         || n.MotivoRejeicao.Contains("SEFAZ 108")
                         || n.MotivoRejeicao.Contains("SEFAZ 109")))
            .Take(300)
            .ToListAsync();

        foreach (var n in recuperar)
            n.RegistrarTransmissao(n.ChaveAcesso!, n.XmlEnvio!);   // volta para 'Transmitindo'
        if (recuperar.Count > 0)
            await db.SaveChangesAsync();

        foreach (var id in pendentes)
            jobs.Enqueue<TransmitirNFCeJob>(j => j.ExecutarAsync(id));
        foreach (var n in recuperar)
            jobs.Enqueue<TransmitirNFCeJob>(j => j.ExecutarAsync(n.Id));

        var total = pendentes.Count + recuperar.Count;
        if (total > 0)
            logger.LogInformation("[NFCe] Reenfileiradas {N} nota(s) ({P} presas + {R} recuperadas de comunicação).",
                total, pendentes.Count, recuperar.Count);
    }
}
