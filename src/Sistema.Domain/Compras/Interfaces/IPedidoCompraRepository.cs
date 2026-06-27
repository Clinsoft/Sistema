using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Compras.Interfaces;

public interface IPedidoCompraRepository : IRepository<PedidoCompra>
{
    Task<PedidoCompra?> ObterComItensAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoCompra>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default);
    Task<string> ProximoNumeroAsync(Guid empresaId, CancellationToken ct = default);
}
