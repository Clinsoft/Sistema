using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Descarta vendas que ficaram "Em Aberto" (iniciadas mas não finalizadas) há mais
/// de X horas — normalmente cliques repetidos/abandono no PDV. Remove itens e
/// pagamentos vinculados. Roda de hora em hora.
/// </summary>
public class LimparVendasAbertasJob(SistemaDbContext db, ILogger<LimparVendasAbertasJob> logger)
{
    // Uma venda em aberto por mais que isso é considerada abandonada.
    private const int HorasLimite = 6;

    [AutomaticRetry(Attempts = 1)]
    public async Task ExecutarAsync()
    {
        var corte = DateTime.Now.AddHours(-HorasLimite);

        // Remove filhos primeiro (FK), depois as vendas — tudo pelo mesmo critério.
        await db.Database.ExecuteSqlRawAsync(
            "DELETE pv FROM PagamentosVenda pv JOIN Vendas v ON v.Id = pv.VendaId " +
            "WHERE v.Status = 'EmAberto' AND v.DataHora < {0}", corte);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE iv FROM ItensVenda iv JOIN Vendas v ON v.Id = iv.VendaId " +
            "WHERE v.Status = 'EmAberto' AND v.DataHora < {0}", corte);
        var n = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM Vendas WHERE Status = 'EmAberto' AND DataHora < {0}", corte);

        if (n > 0)
            logger.LogInformation("[Vendas] {N} venda(s) em aberto antiga(s) descartada(s).", n);
    }
}
