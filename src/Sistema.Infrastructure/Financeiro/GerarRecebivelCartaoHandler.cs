using MediatR;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Events;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Financeiro;

/// <summary>
/// Ao finalizar a venda, gera um Recebível de Cartão para cada pagamento em
/// cartão que tenha operadora informada: calcula a taxa (débito, crédito à vista
/// ou parcelado), o valor líquido e a data prevista de repasse.
/// </summary>
public class GerarRecebivelCartaoHandler(SistemaDbContext db)
    : INotificationHandler<VendaFinalizadaEvent>
{
    public async Task Handle(VendaFinalizadaEvent evt, CancellationToken ct)
    {
        var pagamentosCartao = evt.Pagamentos
            .Where(p => p.OperadoraCartaoId is not null &&
                        p.Forma is FormaPagamento.CartaoCredito or FormaPagamento.CartaoDebito)
            .ToList();
        if (pagamentosCartao.Count == 0) return;

        var operadoraIds = pagamentosCartao.Select(p => p.OperadoraCartaoId!.Value).Distinct().ToList();
        var operadoras = await db.OperadorasCartao.AsNoTracking()
            .Where(o => operadoraIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, ct);

        var hoje = DateTime.Today;

        foreach (var pag in pagamentosCartao)
        {
            if (!operadoras.TryGetValue(pag.OperadoraCartaoId!.Value, out var op)) continue;

            var credito = pag.Forma == FormaPagamento.CartaoCredito;
            var parcelado = credito && pag.Parcelas >= 2;

            var (taxa, prazoDias, forma) = (credito, parcelado) switch
            {
                (false, _)    => (op.TaxaDebito, op.PrazoDiasDebito, "Débito"),
                (true, false) => (op.TaxaCreditoVista, op.PrazoDiasCreditoVista, "Crédito à vista"),
                (true, true)  => (op.TaxaCreditoParcelado, op.PrazoDiasCreditoParcelado,
                                  $"Crédito {pag.Parcelas}x"),
            };

            db.ReceiveisCartao.Add(RecebivelCartao.Criar(
                evt.EmpresaId, op.Id, evt.VendaId,
                forma, credito ? pag.Parcelas : 1,
                valorBruto: pag.Valor, taxa: taxa,
                dataTransacao: hoje,
                dataPrevistaRepasse: hoje.AddDays(prazoDias)));
        }

        await db.SaveChangesAsync(ct);
    }
}
