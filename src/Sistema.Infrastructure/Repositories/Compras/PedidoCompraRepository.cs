using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Compras.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Compras;

public class PedidoCompraRepository(SistemaDbContext db) : BaseRepository<PedidoCompra>(db), IPedidoCompraRepository
{
    public async Task<PedidoCompra?> ObterComItensAsync(Guid id, CancellationToken ct = default)
        => await _set.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PedidoCompra>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Include(p => p.Itens)
            .Where(p => p.EmpresaId == empresaId && p.DataPedido >= inicio && p.DataPedido <= fim)
            .OrderByDescending(p => p.DataPedido)
            .ToListAsync(ct);

    public async Task<string> ProximoNumeroAsync(Guid empresaId, CancellationToken ct = default)
    {
        var ultimo = await _set
            .Where(p => p.EmpresaId == empresaId)
            .OrderByDescending(p => p.CriadoEm)
            .Select(p => p.Numero)
            .FirstOrDefaultAsync(ct);
        return int.TryParse(ultimo, out var n) ? (n + 1).ToString("D6") : "000001";
    }
}
