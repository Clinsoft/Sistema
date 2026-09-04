using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Marketing.Entities;
using Sistema.Domain.Marketing.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Marketing;

public class ClubeRepository(SistemaDbContext db) : IClubeRepository
{
    // Rastreado (sem AsNoTracking): permite RegistrarCompra e persistir no SalvarAsync do UoW.
    public async Task<MembroClube?> ObterMembroAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default)
        => await db.MembrosClube.FirstOrDefaultAsync(m => m.EmpresaId == empresaId && m.ClienteId == clienteId, ct);

    public async Task AdicionarMembroAsync(MembroClube membro, CancellationToken ct = default)
        => await db.MembrosClube.AddAsync(membro, ct);
}
