using MediatR;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Queries;

public record VendaResumoDto(
    Guid Id, string Numero, DateTime DataHora, string Status,
    Guid? ClienteId, decimal Total, decimal TotalPago, decimal Troco, int QtdItens);

public record ListarVendasQuery(Guid EmpresaId, DateTime Inicio, DateTime Fim)
    : IRequest<IReadOnlyList<VendaResumoDto>>;

public class ListarVendasHandler(IVendaRepository repo)
    : IRequestHandler<ListarVendasQuery, IReadOnlyList<VendaResumoDto>>
{
    public async Task<IReadOnlyList<VendaResumoDto>> Handle(ListarVendasQuery q, CancellationToken ct)
    {
        var vendas = await repo.ListarPorPeriodoAsync(q.EmpresaId, q.Inicio, q.Fim, ct);
        return vendas.Select(v => new VendaResumoDto(
            v.Id, v.Numero, v.DataHora, v.Status.ToString(),
            v.ClienteId, v.Total, v.TotalPago, v.Troco, v.Itens.Count
        )).ToList();
    }
}
