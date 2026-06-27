using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.Domain.Vendas.Interfaces;

public interface IDevolucaoVendaRepository : IRepository<DevolucaoVenda>
{
    Task<DevolucaoVenda?> ObterComItensAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DevolucaoVenda>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default);
}
