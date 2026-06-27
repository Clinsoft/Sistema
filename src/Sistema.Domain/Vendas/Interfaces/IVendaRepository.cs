using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Vendas.Interfaces;

public interface IVendaRepository : IRepository<Venda>
{
    Task<Venda?> ObterComItensAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Venda>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default);
    Task<string> ProximoNumeroAsync(Guid empresaId, CancellationToken ct = default);
    Task<decimal> TotalVendidasAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default);
}
