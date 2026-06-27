using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Estoque;

public class MovimentacaoEstoqueRepository(SistemaDbContext db) : BaseRepository<MovimentacaoEstoque>(db), IMovimentacaoEstoqueRepository
{
    public async Task<IReadOnlyList<MovimentacaoEstoque>> ListarPorProdutoAsync(Guid empresaId, Guid produtoId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.ProdutoId == produtoId)
            .OrderByDescending(m => m.CriadoEm)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MovimentacaoEstoque>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.CriadoEm >= inicio && m.CriadoEm <= fim)
            .OrderByDescending(m => m.CriadoEm)
            .ToListAsync(ct);
}
