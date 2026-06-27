using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Cadastros.Interfaces;

public interface IUsuarioRepository : IRepository<Usuario>
{
    Task<Usuario?> ObterPorEmailAsync(Guid? empresaId, string email, CancellationToken ct = default);
}
