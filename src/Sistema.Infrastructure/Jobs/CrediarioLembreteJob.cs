using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Crediario.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

public class CrediarioLembreteJob(SistemaDbContext db, ILogger<CrediarioLembreteJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecutarAsync()
    {
        var amanha = DateTime.Today.AddDays(1);
        var hoje = DateTime.Today;

        // Parcelas que vencem amanhã ou hoje — enviar lembrete
        var parcelas = await db.ParcelasCrediario.AsNoTracking()
            .Where(p => p.Status == StatusParcela.EmAberto
                && (p.DataVencimento.Date == hoje || p.DataVencimento.Date == amanha))
            .Join(db.Crediarios, p => p.CrediarioId, c => c.Id, (p, c) => new { p, c })
            .Join(db.Clientes, x => x.c.ClienteId, cl => cl.Id,
                (x, cl) => new
                {
                    x.p.Id, x.p.Valor, x.p.DataVencimento,
                    CrediarioNumero = x.c.Numero,
                    ClienteNome = cl.Nome, cl.Celular, cl.Email
                })
            .ToListAsync();

        foreach (var parcela in parcelas)
        {
            var diasRestantes = (parcela.DataVencimento.Date - DateTime.Today).Days;
            var prefixo = diasRestantes == 0 ? "VENCE HOJE" : "VENCE AMANHÃ";

            logger.LogInformation(
                "[CREDIÁRIO LEMBRETE] {Prefixo} | Cliente: {Nome} | Crediário: {Num} | Valor: R${Valor:F2} | Tel: {Tel}",
                prefixo, parcela.ClienteNome, parcela.CrediarioNumero, parcela.Valor, parcela.Celular);

            // TODO Fase 2: integrar com WhatsApp provider para envio automático
        }
    }
}
