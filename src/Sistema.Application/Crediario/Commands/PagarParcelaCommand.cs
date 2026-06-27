using MediatR;
using Sistema.Domain.Crediario.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Crediario.Commands;

public record PagarParcelaCommand(Guid ParcelaId, decimal ValorPago) : IRequest<PagarParcelaResult>;

public record PagarParcelaResult(bool Liquidado, decimal SaldoRestante);

public class PagarParcelaHandler(IParcelaCrediarioRepository parcelaRepo, ICrediarioRepository crediarioRepo, IUnitOfWork uow)
    : IRequestHandler<PagarParcelaCommand, PagarParcelaResult>
{
    public async Task<PagarParcelaResult> Handle(PagarParcelaCommand cmd, CancellationToken ct)
    {
        var parcela = await parcelaRepo.ObterComCrediarioAsync(cmd.ParcelaId, ct)
            ?? throw new KeyNotFoundException("Parcela não encontrada.");

        parcela.Pagar(cmd.ValorPago);
        await uow.SalvarAsync(ct);

        var crediario = await crediarioRepo.ObterComParcelasAsync(parcela.CrediarioId, ct);
        var saldo = crediario?.SaldoDevedor() ?? 0;
        var liquidado = saldo == 0;

        if (liquidado && crediario is not null)
        {
            crediario.Liquidar();
            await uow.SalvarAsync(ct);
        }

        return new PagarParcelaResult(liquidado, saldo);
    }
}
