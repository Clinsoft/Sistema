using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Transmite UMA NFC-e à SEFAZ em SEGUNDO PLANO (fora da finalização da venda).
/// A venda já finalizou e a nota está salva como 'Transmitindo' — aqui ela é
/// autorizada (ou rejeitada). Se a SEFAZ estiver lenta/fora, a transmissão lança
/// exceção e o Hangfire re-tenta com backoff (o job persiste em SQL, sobrevive a
/// restart). Assim uma SEFAZ instável NUNCA trava a venda.
/// </summary>
public class TransmitirNFCeJob(
    SistemaDbContext db,
    INFeTransmissaoService transmissao,
    ILogger<TransmitirNFCeJob> logger)
{
    // Backoff: 1min, 3min, 10min, 30min, 1h, 2h — cobre instabilidades de SEFAZ.
    // Depois disso, a nota fica 'Transmitindo' e o RetransmitirNotasPendentesJob assume.
    [AutomaticRetry(Attempts = 6, DelaysInSeconds = new[] { 60, 180, 600, 1800, 3600, 7200 })]
    public async Task ExecutarAsync(Guid notaId)
    {
        var nota = await db.NotasFiscais.FirstOrDefaultAsync(n => n.Id == notaId);
        if (nota is null) { logger.LogWarning("[NFCe] Nota {Id} não encontrada.", notaId); return; }

        // Idempotente: se já resolveu, não reenvia (evita duplicidade na SEFAZ).
        if (nota.Status is StatusNF.Autorizada or StatusNF.Cancelada or StatusNF.Inutilizada
            or StatusNF.DenegadaSefaz or StatusNF.Rejeitada)
            return;

        if (string.IsNullOrWhiteSpace(nota.XmlEnvio))
        {
            logger.LogWarning("[NFCe] Nota {Id} sem XML assinado — nada a transmitir.", notaId);
            return;
        }

        var config = await db.ConfiguracoesFiscais
            .FirstOrDefaultAsync(c => c.EmpresaId == nota.EmpresaId);
        if (config is null)
        {
            logger.LogWarning("[NFCe] Configuração fiscal ausente para empresa {E}.", nota.EmpresaId);
            return;
        }

        // Token do JOB (não o da requisição).
        var resultado = await transmissao.TransmitirAsync(nota.XmlEnvio, config, CancellationToken.None);

        if (resultado.Autorizada)
        {
            nota.RegistrarAutorizacao(resultado.Protocolo!, resultado.XmlRetorno ?? nota.XmlEnvio);
            await db.SaveChangesAsync();
            logger.LogInformation("[NFCe] Nota {Id} AUTORIZADA (protocolo {P}).", notaId, resultado.Protocolo);
            return;
        }

        // Falha TRANSITÓRIA (SEFAZ fora/lenta/paralisada): NÃO marca rejeitada — mantém
        // 'Transmitindo' e lança para o Hangfire re-tentar (e a varredura reenfileirar).
        if (EhFalhaTransitoria(resultado.MotivoRejeicao))
        {
            logger.LogWarning("[NFCe] Nota {Id} falha transitória (vai re-tentar): {M}", notaId, resultado.MotivoRejeicao);
            throw new InvalidOperationException($"Falha transitória na transmissão: {resultado.MotivoRejeicao}");
        }

        // Rejeição DEFINITIVA da SEFAZ (schema/dados/regra) — precisa de correção humana.
        nota.RegistrarRejeicao(resultado.MotivoRejeicao ?? "Rejeitada pela SEFAZ", resultado.XmlRetorno ?? "");
        await db.SaveChangesAsync();
        logger.LogWarning("[NFCe] Nota {Id} REJEITADA: {M}", notaId, resultado.MotivoRejeicao);
    }

    /// <summary>
    /// Falha que deve ser RE-TENTADA (não é rejeição definitiva): erro de comunicação/timeout,
    /// resposta ilegível, ou SEFAZ paralisada (cStat 108/109). Sem motivo → re-tenta por segurança.
    /// </summary>
    internal static bool EhFalhaTransitoria(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) return true;
        return motivo.StartsWith("Erro de comunicação", StringComparison.OrdinalIgnoreCase)
            || motivo.StartsWith("Erro ao parsear resposta", StringComparison.OrdinalIgnoreCase)
            || motivo.Contains("SEFAZ 108") || motivo.Contains("SEFAZ 109");
    }
}
