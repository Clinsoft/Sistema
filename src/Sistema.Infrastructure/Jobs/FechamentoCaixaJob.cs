using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Fecha automaticamente as sessões de caixa (PDV) que ficaram abertas de dias anteriores —
/// quando o operador esqueceu de fechar. Roda de madrugada. Fecha com o saldo ESPERADO
/// calculado pelo sistema (dinheiro líquido + suprimentos − sangrias sobre o saldo de abertura),
/// deixando diferença zero e uma observação de que foi fechamento automático.
/// Vários caixas abertos na mesma loja no mesmo dia são permitidos — este job só age no que
/// atravessou o dia sem ser fechado.
/// </summary>
public class FechamentoCaixaJob(
    SistemaDbContext db, IVendaRepository vendaRepo, IPDVSessaoRepository repo,
    IUnitOfWork uow, ILogger<FechamentoCaixaJob> logger)
{
    public async Task ExecutarAsync()
    {
        // Servidor em America/Sao_Paulo; Abertura é gravada com DateTime.Now (BRT), então
        // comparamos com a data local de hoje. Sessões abertas ANTES de hoje = deixadas abertas.
        var hoje = DateTime.Now.Date;

        var ids = await db.PDVSessoes.AsNoTracking()
            .Where(s => s.Status == StatusSessao.Aberta && s.Abertura < hoje)
            .Select(s => s.Id)
            .ToListAsync();
        if (ids.Count == 0) return;

        int fechadas = 0;
        foreach (var id in ids)
        {
            var s = await repo.ObterPorIdAsync(id);
            if (s is null || s.Status == StatusSessao.Fechada) continue;

            var (dinheiro, troco) = await vendaRepo.TotaisDinheiroAsync(
                s.EmpresaId, s.Abertura, DateTime.Now, s.UsuarioId, s.LocalEstoqueId);
            var saldoEsperado = Math.Round(
                s.SaldoAbertura + (dinheiro - troco) + s.TotalSuprimentos - s.TotalSangrias, 2);

            s.Fechar(saldoEsperado,
                $"Fechamento automático — caixa deixado aberto desde {s.Abertura:dd/MM/yyyy HH:mm}.");
            repo.Atualizar(s);
            fechadas++;
        }

        if (fechadas > 0)
        {
            await uow.SalvarAsync();
            logger.LogInformation("[FechamentoCaixa] {N} caixa(s) deixado(s) aberto(s) fechado(s) automaticamente.", fechadas);
        }
    }
}
