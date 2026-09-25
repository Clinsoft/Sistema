using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Assinaturas;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Branding;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Régua de lembretes de vencimento (cobrança amigável): avisa o lojista por e-mail antes do
/// fim do trial e do vencimento da assinatura, e quando fica bloqueado. Um aviso por ETAPA
/// (não repete todo dia). Roda 1x/dia. Se o SMTP não estiver configurado, o e-mail só é logado.
/// </summary>
public class AssinaturaLembreteJob(SistemaDbContext db, IEmailService email,
    ILogger<AssinaturaLembreteJob> logger)
{
    [AutomaticRetry(Attempts = 1)]
    public async Task ExecutarAsync()
    {
        var hoje = DateTime.UtcNow.Date;
        var b = BrandingRuntime.Atual;
        var linkPagar = b.PublicBaseUrl.TrimEnd('/') + "/#/assinatura";

        var itens = await (from a in db.Assinaturas
                           join e in db.Empresas on a.EmpresaId equals e.Id
                           where a.Status == StatusAssinatura.Trial
                              || a.Status == StatusAssinatura.Ativa
                              || a.Status == StatusAssinatura.Bloqueada
                           select new { A = a, e.NomeFantasia, e.Email }).ToListAsync();

        var enviados = 0;
        foreach (var it in itens)
        {
            var etapa = Etapa(it.A, hoje);
            if (etapa is null || etapa == it.A.UltimoLembrete) continue;
            if (string.IsNullOrWhiteSpace(it.Email)) { it.A.RegistrarLembrete(etapa); continue; }

            var (assunto, corpo) = Mensagem(etapa, it.NomeFantasia, b.Nome, linkPagar);
            try
            {
                await email.EnviarAsync(it.Email!, assunto, corpo);
                it.A.RegistrarLembrete(etapa);
                enviados++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao enviar lembrete para {Email}", it.Email);
            }
        }

        if (enviados > 0 || itens.Count > 0) await db.SaveChangesAsync();
        logger.LogInformation("Lembretes de assinatura enviados: {N}", enviados);
    }

    private static string? Etapa(Assinatura a, DateTime hoje) => a.Status switch
    {
        StatusAssinatura.Trial when a.TrialAte is DateTime t =>
            (t.Date - hoje).Days switch { <= 0 => "trial-fim", <= 3 => "trial-3d", _ => null },
        StatusAssinatura.Ativa when a.ProximoVencimento is DateTime v =>
            (v.Date - hoje).Days switch { < 0 => "venc-atraso", 0 => "venc-hoje", <= 3 => "venc-3d", _ => null },
        StatusAssinatura.Bloqueada => "bloqueada",
        _ => null,
    };

    private static (string Assunto, string Corpo) Mensagem(string etapa, string loja, string marca, string link)
    {
        var (titulo, texto) = etapa switch
        {
            "trial-3d" => ("Seu teste grátis está acabando",
                "faltam poucos dias do seu teste grátis. Assine agora e continue sem interrupção."),
            "trial-fim" => ("Seu teste grátis termina hoje",
                "hoje é o último dia do seu teste grátis. Assine para não perder o acesso ao sistema."),
            "venc-3d" => ("Sua assinatura vence em breve",
                "sua assinatura está prestes a vencer. Garanta o pagamento para manter tudo funcionando."),
            "venc-hoje" => ("Sua assinatura vence hoje",
                "sua assinatura vence hoje. Efetue o pagamento para não perder o acesso."),
            "venc-atraso" => ("Pagamento em atraso",
                "identificamos um atraso no pagamento da sua assinatura. Regularize para evitar o bloqueio."),
            "bloqueada" => ("Sua assinatura está bloqueada",
                "seu acesso foi bloqueado por falta de pagamento. Assine para reativar — seus dados estão salvos."),
            _ => ("Aviso da sua assinatura", "há uma atualização sobre a sua assinatura."),
        };

        var corpo = $"""
            <div style="font-family:sans-serif;max-width:520px;margin:auto;padding:24px">
              <h2 style="color:#184460;margin:0 0 4px">{marca}</h2>
              <h3 style="color:#184460;margin:12px 0">{titulo}</h3>
              <p>Olá, <strong>{loja}</strong>!</p>
              <p>{texto}</p>
              <p style="margin:24px 0">
                <a href="{link}" style="background:#2FA063;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:bold">Assinar / pagar agora</a>
              </p>
              <p style="color:#888;font-size:13px">Se já efetuou o pagamento, desconsidere este aviso.</p>
            </div>
            """;
        return ($"{marca} — {titulo}", corpo);
    }
}
