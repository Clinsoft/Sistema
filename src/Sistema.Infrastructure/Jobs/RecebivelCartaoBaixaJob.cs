using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Baixa automática dos recebíveis de cartão: quando a data prevista de crédito
/// (D + prazo da operadora) chega, o dinheiro caiu na conta — então o recebível
/// pendente passa para "Recebido", usando a própria data prevista como data de repasse.
/// </summary>
public class RecebivelCartaoBaixaJob(SistemaDbContext db, ILogger<RecebivelCartaoBaixaJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecutarAsync()
    {
        var hoje = DateTime.Today;

        var vencidos = await db.ReceiveisCartao
            .Where(r => r.Status == StatusRecebivelCartao.Pendente
                     && r.DataPrevistaRepasse <= hoje)
            .ToListAsync();

        if (vencidos.Count == 0) return;

        foreach (var r in vencidos)
            r.MarcarRecebido(r.DataPrevistaRepasse);

        await db.SaveChangesAsync();
        logger.LogInformation("[RECEBIVEL-CARTAO] {Qtd} recebível(is) marcado(s) como Recebido pela data de crédito.",
            vencidos.Count);
    }
}
