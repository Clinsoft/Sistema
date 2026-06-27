using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Estoque.Interfaces;

public interface ILoteRepository : IRepository<Lote>
{
    Task<IReadOnlyList<Lote>> ListarPorProdutoAsync(Guid empresaId, Guid produtoId, CancellationToken ct = default);
    Task<IReadOnlyList<Lote>> ListarVencidosOuProximosAsync(Guid empresaId, int diasAlerta, CancellationToken ct = default);
}
