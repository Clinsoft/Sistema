namespace Sistema.Domain.Shared.Interfaces;

public interface IUnitOfWork
{
    Task<int> SalvarAsync(CancellationToken ct = default);
}
