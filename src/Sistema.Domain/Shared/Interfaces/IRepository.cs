using Sistema.Domain.Shared.Primitives;
using System.Linq.Expressions;

namespace Sistema.Domain.Shared.Interfaces;

public interface IRepository<T> where T : Entity
{
    Task<T?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListarAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AdicionarAsync(T entity, CancellationToken ct = default);
    void Atualizar(T entity);
    void Remover(T entity);
    Task<bool> ExisteAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> ContarAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
