using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Crediario.Entities;
using Sistema.Domain.Crediario.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Crediario;

public class CrediarioRepository(SistemaDbContext db) : BaseRepository<Domain.Crediario.Entities.Crediario>(db), ICrediarioRepository
{
    public async Task<string> ProximoNumeroAsync(Guid empresaId, CancellationToken ct = default)
    {
        var ultimo = await _set
            .Where(c => c.EmpresaId == empresaId)
            .OrderByDescending(c => c.CriadoEm)
            .Select(c => c.Numero)
            .FirstOrDefaultAsync(ct);
        return int.TryParse(ultimo, out var n) ? (n + 1).ToString("D6") : "000001";
    }

    public async Task<IReadOnlyList<Domain.Crediario.Entities.Crediario>> ListarPorClienteAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Include(c => c.Parcelas)
            .Where(c => c.EmpresaId == empresaId && c.ClienteId == clienteId)
            .OrderByDescending(c => c.CriadoEm)
            .ToListAsync(ct);

    public async Task<Domain.Crediario.Entities.Crediario?> ObterComParcelasAsync(Guid id, CancellationToken ct = default)
        => await _set
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
}

public class ParcelaCrediarioRepository(SistemaDbContext db) : BaseRepository<ParcelaCrediario>(db), IParcelaCrediarioRepository
{
    public async Task<ParcelaCrediario?> ObterComCrediarioAsync(Guid parcelaId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(p => p.Id == parcelaId, ct);
}
