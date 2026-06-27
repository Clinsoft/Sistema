using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

public class EstoqueAlertaJob(SistemaDbContext db, ILogger<EstoqueAlertaJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecutarAsync()
    {
        var empresas = await db.Empresas.AsNoTracking().Select(e => e.Id).ToListAsync();

        foreach (var empresaId in empresas)
        {
            var produtos = await db.Produtos.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId && p.Ativo && p.EstoqueAtual <= p.EstoqueMinimo)
                .Select(p => new { p.Codigo, p.Descricao, p.EstoqueAtual, p.EstoqueMinimo })
                .ToListAsync();

            if (!produtos.Any()) continue;

            logger.LogWarning("[ESTOQUE ABAIXO DO MÍNIMO] Empresa {EmpresaId}: {Qtd} produtos",
                empresaId, produtos.Count);

            foreach (var p in produtos)
                logger.LogWarning("  → {Codigo} - {Descricao}: atual={Atual} / mínimo={Minimo}",
                    p.Codigo, p.Descricao, p.EstoqueAtual, p.EstoqueMinimo);
        }
    }
}
