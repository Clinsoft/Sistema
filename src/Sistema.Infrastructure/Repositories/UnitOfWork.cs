using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories;

public class UnitOfWork(SistemaDbContext db) : IUnitOfWork
{
    public async Task<int> SalvarAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
