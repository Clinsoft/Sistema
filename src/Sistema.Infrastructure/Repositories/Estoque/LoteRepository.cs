using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Estoque;

public class LoteRepository(SistemaDbContext db) : BaseRepository<Lote>(db), ILoteRepository
{
    // Lista TODOS os lotes do produto (inclusive com quantidade zerada), para a ficha do produto.
    public async Task<IReadOnlyList<Lote>> ListarPorProdutoAsync(Guid empresaId, Guid produtoId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.ProdutoId == produtoId)
            .OrderBy(l => l.DataValidade)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Lote>> ListarVencidosOuProximosAsync(Guid empresaId, int diasAlerta, CancellationToken ct = default)
    {
        var limite = DateTime.Today.AddDays(diasAlerta);
        return await _set.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Quantidade > 0
                && l.DataValidade.HasValue && l.DataValidade <= limite)
            .OrderBy(l => l.DataValidade)
            .ToListAsync(ct);
    }
}
