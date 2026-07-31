using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Vendas;

public class DevolucaoVendaRepository(SistemaDbContext db) : BaseRepository<DevolucaoVenda>(db), IDevolucaoVendaRepository
{
    public async Task<DevolucaoVenda?> ObterComItensAsync(Guid id, CancellationToken ct = default)
        => await _set.Include(d => d.Itens).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<DevolucaoVenda>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId && d.DataHora >= inicio && d.DataHora < fim.AddDays(1))
            .OrderByDescending(d => d.DataHora)
            .ToListAsync(ct);
}
