using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Cadastros;

public class UsuarioRepository(SistemaDbContext db) : BaseRepository<Usuario>(db), IUsuarioRepository
{
    public async Task<Usuario?> ObterPorEmailAsync(Guid? empresaId, string email, CancellationToken ct = default)
        => empresaId.HasValue
            ? await _set.FirstOrDefaultAsync(u => u.EmpresaId == empresaId.Value && u.Email == email && u.Ativo, ct)
            : await _set.FirstOrDefaultAsync(u => u.Email == email && u.Ativo, ct);
}
