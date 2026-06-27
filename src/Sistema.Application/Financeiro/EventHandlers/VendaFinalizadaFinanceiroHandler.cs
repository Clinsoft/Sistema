using MediatR;
using Sistema.Domain.Vendas.Events;

namespace Sistema.Application.Financeiro.EventHandlers;

/// <summary>
/// Placeholder — integração financeira automática da venda.
/// A conta a receber de Crediário é gerada no AbrirCrediarioCommand.
/// Pagamentos à vista (dinheiro, pix, cartão) são capturados no fechamento de sessão PDV.
/// Este handler existe para extensões futuras (ex: DRE em tempo real).
/// </summary>
public class VendaFinalizadaFinanceiroHandler : INotificationHandler<VendaFinalizadaEvent>
{
    public Task Handle(VendaFinalizadaEvent notification, CancellationToken ct)
        => Task.CompletedTask;
}
