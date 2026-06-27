using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Shared.Primitives;
using Sistema.Infrastructure.Data;
using System.Linq.Expressions;

namespace Sistema.Infrastructure.Repositories;

public class BaseRepository<T>(SistemaDbContext db) : IRepository<T> where T : Entity
{
    protected readonly SistemaDbContext _db = db;
    protected readonly DbSet<T> _set = db.Set<T>();

    public async Task<T?> ObterPorIdAsync(Guid id, CancellationToken ct = default)
        => await _set.FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> ListarAsync(CancellationToken ct = default)
        => await _set.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> ListarAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public async Task AdicionarAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);

    public void Atualizar(T entity)
    {
        // Se já rastreada, o ChangeTracker detecta as mudanças automaticamente.
        // Chamar Update() em entidades rastreadas com filhos novos marca esses filhos
        // como Modified (em vez de Added), causando DbUpdateConcurrencyException.
        if (_db.Entry(entity).State == EntityState.Detached)
            _set.Update(entity);
    }

    public void Remover(T entity) => _set.Remove(entity);

    public async Task<bool> ExisteAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.AnyAsync(predicate, ct);

    public async Task<int> ContarAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.CountAsync(predicate, ct);
}
