using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Vendas;

public class PDVSessaoRepository(SistemaDbContext db) : BaseRepository<PDVSessao>(db), IPDVSessaoRepository
{
    public async Task<PDVSessao?> ObterSessaoAbertaAsync(Guid empresaId, Guid usuarioId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(
            s => s.EmpresaId == empresaId && s.UsuarioId == usuarioId && s.Status == StatusSessao.Aberta, ct);

    public async Task<IReadOnlyList<PDVSessao>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.Abertura >= inicio && s.Abertura <= fim)
            .OrderByDescending(s => s.Abertura)
            .ToListAsync(ct);
}
