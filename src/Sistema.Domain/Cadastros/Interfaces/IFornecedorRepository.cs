using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Cadastros.Interfaces;

public interface IFornecedorRepository : IRepository<Fornecedor>
{
    Task<IReadOnlyList<Fornecedor>> PesquisarAsync(Guid empresaId, string? termo, CancellationToken ct = default);
}
