using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Estoque;

public class TabelaNutricionalRepository(SistemaDbContext db) : BaseRepository<TabelaNutricional>(db), ITabelaNutricionalRepository
{
    public async Task<TabelaNutricional?> ObterPorProdutoAsync(Guid produtoId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(t => t.ProdutoId == produtoId, ct);
}

public class ReceitaProdutoRepository(SistemaDbContext db) : BaseRepository<ReceitaProduto>(db), IReceitaProdutoRepository
{
    public async Task<IReadOnlyList<ReceitaProduto>> ListarPorProdutoAsync(Guid produtoId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(r => r.ProdutoId == produtoId && r.Ativo)
            .OrderBy(r => r.Ordem)
            .ToListAsync(ct);
}

public class SugestaoProdutoRepository(SistemaDbContext db) : BaseRepository<SugestaoProduto>(db), ISugestaoProdutoRepository
{
    public async Task<IReadOnlyList<SugestaoProduto>> ListarPorProdutoAsync(Guid produtoId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(s => s.ProdutoId == produtoId && s.Ativo)
            .OrderBy(s => s.Ordem)
            .ToListAsync(ct);
}
