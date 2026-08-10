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
        var hoje = DateTime.Today;

        // Pix recebido na maquininha: taxa 0, atribuído à operadora "Pix" da empresa.
        // Todo pagamento em Pix vira recebível para conciliar com o repasse da adquirente.
        var pagamentosPix = evt.Pagamentos.Where(p => p.Forma == FormaPagamento.Pix).ToList();
        if (pagamentosPix.Count > 0)
        {
            var opPix = await db.OperadorasCartao.AsNoTracking()
                .FirstOrDefaultAsync(o => o.EmpresaId == evt.EmpresaId && o.EhPix && o.Ativo, ct);
            if (opPix is not null)
            {
                foreach (var pag in pagamentosPix)
                    db.ReceiveisCartao.Add(RecebivelCartao.Criar(
                        evt.EmpresaId, opPix.Id, evt.VendaId,
                        "Pix", 1, valorBruto: pag.Valor, taxa: opPix.TaxaPix,
                        dataTransacao: hoje,
                        dataPrevistaRepasse: hoje.AddDays(opPix.PrazoDiasPix)));
                await db.SaveChangesAsync(ct);
            }
        }

        var pagamentosCartao = evt.Pagamentos
            .Where(p => p.OperadoraCartaoId is not null &&
                        p.Forma is FormaPagamento.CartaoCredito or FormaPagamento.CartaoDebito)
            .ToList();
        if (pagamentosCartao.Count == 0) return;

        var operadoraIds = pagamentosCartao.Select(p => p.OperadoraCartaoId!.Value).Distinct().ToList();
        var operadoras = await db.OperadorasCartao.AsNoTracking()
            .Where(o => operadoraIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, ct);

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

            if (parcelado)
            {
                // Uma linha por parcela: cada parcela cai ~30 dias após a anterior
                // (parcela 1 em D+prazo, parcela 2 em D+prazo+30, ...). Repasse real da operadora.
                var n = pag.Parcelas;
                var valorParcela = Math.Round(pag.Valor / n, 2);
                for (var k = 1; k <= n; k++)
                {
                    // A última parcela absorve a sobra do arredondamento.
                    var valor = k == n ? pag.Valor - valorParcela * (n - 1) : valorParcela;
                    db.ReceiveisCartao.Add(RecebivelCartao.Criar(
                        evt.EmpresaId, op.Id, evt.VendaId,
                        forma, n,
                        valorBruto: valor, taxa: taxa,
                        dataTransacao: hoje,
                        dataPrevistaRepasse: hoje.AddDays(prazoDias + 30 * (k - 1))));
                }
            }
            else
            {
                db.ReceiveisCartao.Add(RecebivelCartao.Criar(
                    evt.EmpresaId, op.Id, evt.VendaId,
                    forma, credito ? pag.Parcelas : 1,
                    valorBruto: pag.Valor, taxa: taxa,
                    dataTransacao: hoje,
                    dataPrevistaRepasse: hoje.AddDays(prazoDias)));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
