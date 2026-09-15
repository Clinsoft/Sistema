using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Desempenho.Entities;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Penaliza o desempenho quando uma conversa do WhatsApp fica SEM resposta por mais de 6 horas
/// de HORÁRIO DE FUNCIONAMENTO (Seg–Sáb 8–21h, Dom 8–14h). Desconta pontos de TODOS os atendentes
/// da loja dona do número. Idempotente: cada mensagem sem resposta gera no máximo uma penalidade
/// por atendente. Roda de hora em hora.
/// </summary>
public class WhatsAppSlaJob(SistemaDbContext db, ILogger<WhatsAppSlaJob> logger)
{
    private const decimal PontosPorOcorrencia = 10m;
    private const int PrazoMinutosUteis = 360;   // 6 horas de funcionamento

    private static readonly TimeZoneInfo Tz = ResolverTz();
    private static TimeZoneInfo ResolverTz()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
    }
    private static DateTime ParaLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tz);

    // Janela de funcionamento do dia (local). Domingo 8–14; Seg–Sáb 8–21.
    private static (DateTime abre, DateTime fecha) Janela(DateTime dia)
    {
        var abre = dia.Date.AddHours(8);
        var fecha = dia.DayOfWeek == DayOfWeek.Sunday ? dia.Date.AddHours(14) : dia.Date.AddHours(21);
        return (abre, fecha);
    }

    /// <summary>Minutos de HORÁRIO DE FUNCIONAMENTO entre dois instantes locais.</summary>
    private static double MinutosUteis(DateTime aLocal, DateTime bLocal)
    {
        if (bLocal <= aLocal) return 0;
        double total = 0;
        for (var dia = aLocal.Date; dia <= bLocal.Date; dia = dia.AddDays(1))
        {
            var (abre, fecha) = Janela(dia);
            var ini = aLocal > abre ? aLocal : abre;
            var fim = bLocal < fecha ? bLocal : fecha;
            if (fim > ini) total += (fim - ini).TotalMinutes;
        }
        return total;
    }

    public async Task ExecutarAsync()
    {
        var agoraLocal = ParaLocal(DateTime.UtcNow);
        var desde = DateTime.UtcNow.AddDays(-10);   // só conversas recentes

        // Lojas com atendentes cadastrados.
        var atendentesPorLoja = (await db.Usuarios.AsNoTracking()
            .Where(u => u.Ativo && u.Perfil == "Atendente" && u.LocalEstoqueId != null)
            .Select(u => new { u.Id, u.EmpresaId, Loja = u.LocalEstoqueId!.Value })
            .ToListAsync())
            .GroupBy(u => u.Loja)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (atendentesPorLoja.Count == 0) return;

        var lojas = atendentesPorLoja.Keys.ToList();

        // Mensagens recentes das conversas dessas lojas.
        var msgs = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.LocalEstoqueId != null && lojas.Contains(m.LocalEstoqueId.Value)
                     && m.DataHora >= desde)
            .Select(m => new { m.Id, m.EmpresaId, Loja = m.LocalEstoqueId!.Value, m.Telefone, m.Direcao, m.DataHora })
            .OrderBy(m => m.DataHora)
            .ToListAsync();

        // Mensagens que já geraram penalidade (para não duplicar).
        var jaPenalizadas = (await db.PenalidadesAtendimentoWhatsApp.AsNoTracking()
            .Where(p => p.DataOcorrencia >= agoraLocal.AddDays(-11))
            .Select(p => p.MensagemId).Distinct().ToListAsync())
            .ToHashSet();

        int novas = 0;
        // Por conversa (loja + telefone).
        foreach (var conv in msgs.GroupBy(m => (m.Loja, m.Telefone)))
        {
            var ordenadas = conv.OrderBy(m => m.DataHora).ToList();
            for (int i = 0; i < ordenadas.Count; i++)
            {
                var m = ordenadas[i];
                if (m.Direcao != DirecaoMensagemWhatsApp.Recebida) continue;
                // Só o INÍCIO de um turno do cliente (mensagem anterior foi enviada por nós, ou é a 1ª).
                if (i > 0 && ordenadas[i - 1].Direcao == DirecaoMensagemWhatsApp.Recebida) continue;
                if (jaPenalizadas.Contains(m.Id)) continue;

                var recebidaLocal = ParaLocal(m.DataHora);
                // Primeira resposta nossa após esta mensagem.
                var resposta = ordenadas.Skip(i + 1)
                    .FirstOrDefault(x => x.Direcao == DirecaoMensagemWhatsApp.Enviada);
                var fimLocal = resposta != null ? ParaLocal(resposta.DataHora) : agoraLocal;

                var minutos = MinutosUteis(recebidaLocal, fimLocal);
                // Só é violação FINAL: respondida (tarde) OU já passou do prazo sem resposta.
                var respondidaTarde = resposta != null && minutos > PrazoMinutosUteis;
                var vencidaSemResposta = resposta == null && minutos > PrazoMinutosUteis;
                if (!respondidaTarde && !vencidaSemResposta) continue;

                var atendentes = atendentesPorLoja[m.Loja];
                foreach (var at in atendentes)
                {
                    db.PenalidadesAtendimentoWhatsApp.Add(PenalidadeAtendimentoWhatsApp.Criar(
                        m.EmpresaId, m.Loja, at.Id, recebidaLocal, m.Telefone, m.Id, PontosPorOcorrencia));
                }
                jaPenalizadas.Add(m.Id);
                novas++;
            }
        }

        if (novas > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("[WhatsAppSLA] {N} conversas sem resposta em 6h úteis penalizadas.", novas);
        }
    }
}
