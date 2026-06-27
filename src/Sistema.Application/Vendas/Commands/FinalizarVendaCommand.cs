using MediatR;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Commands;

public record PagamentoDto(string Forma, decimal Valor, int Parcelas = 1, string? Descricao = null);

public record FinalizarVendaCommand(
    Guid VendaId,
    IList<PagamentoDto> Pagamentos,
    string? CpfCnpjConsumidor = null) : IRequest<FinalizarVendaResult>;

/// <summary>
/// Dados para o PDV exibir comprovante e QR Code.
/// NotaFiscalId e QrCode ficam nulos enquanto certificado A1 não estiver configurado.
/// </summary>
public record FinalizarVendaResult(
    string Numero,
    decimal Total,
    decimal Troco,
    Guid? NotaFiscalId = null,
    string? QrCode = null,
    string? ChaveAcesso = null);

public class FinalizarVendaHandler(IVendaRepository repo, IUnitOfWork uow)
    : IRequestHandler<FinalizarVendaCommand, FinalizarVendaResult>
{
    public async Task<FinalizarVendaResult> Handle(FinalizarVendaCommand cmd, CancellationToken ct)
    {
        var venda = await repo.ObterComItensAsync(cmd.VendaId, ct)
            ?? throw new KeyNotFoundException("Venda não encontrada.");

        if (!string.IsNullOrWhiteSpace(cmd.CpfCnpjConsumidor))
            venda.InformarCpfCnpjConsumidor(cmd.CpfCnpjConsumidor);

        foreach (var p in cmd.Pagamentos)
        {
            var forma = Enum.Parse<FormaPagamento>(p.Forma);
            venda.AdicionarPagamento(forma, p.Valor, p.Parcelas, p.Descricao);
        }

        venda.Finalizar();
        repo.Atualizar(venda);
        await uow.SalvarAsync(ct);

        // NotaFiscalId preenchido pelo EmitirNFCeHandler após publicação do evento
        return new FinalizarVendaResult(venda.Numero, venda.Total, venda.Troco,
            venda.NotaFiscalId);
    }
}
