using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Vendas;

public class VendaRepository(SistemaDbContext db) : BaseRepository<Venda>(db), IVendaRepository
{
    public async Task<Venda?> ObterComItensAsync(Guid id, CancellationToken ct = default)
        => await _set
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IReadOnlyList<Venda>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default)
    {
        // 'fim' chega como a data (00:00). Incluir o dia inteiro do fim, senão as
        // vendas de hoje (que têm hora > 00:00) ficavam de fora do histórico.
        var fimExclusivo = fim.Date.AddDays(1);
        return await _set.AsNoTracking()
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .Where(v => v.EmpresaId == empresaId && v.DataHora >= inicio.Date && v.DataHora < fimExclusivo)
            .OrderByDescending(v => v.DataHora)
            .ToListAsync(ct);
    }

    public async Task<string> ProximoNumeroAsync(Guid empresaId, CancellationToken ct = default)
    {
        var ultimo = await _set
            .Where(v => v.EmpresaId == empresaId)
            .OrderByDescending(v => v.CriadoEm)
            .Select(v => v.Numero)
            .FirstOrDefaultAsync(ct);

        if (ultimo is null) return "000001";
        return int.TryParse(ultimo, out var n) ? (n + 1).ToString("D6") : "000001";
    }

    public async Task<decimal> TotalVendidasAsync(Guid empresaId, DateTime inicio, DateTime fim,
        Guid? usuarioId = null, Guid? localEstoqueId = null, CancellationToken ct = default)
    {
        // Janela EXATA da sessão (não o dia inteiro) e, quando informado, apenas o
        // operador/caixa da sessão — senão um caixa recém-aberto somava vendas de outros.
        var q = _set.Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
            && v.DataHora >= inicio && v.DataHora <= fim);
        if (usuarioId.HasValue) q = q.Where(v => v.UsuarioId == usuarioId.Value);
        if (localEstoqueId.HasValue) q = q.Where(v => v.LocalEstoqueId == localEstoqueId.Value);
        return await q.SumAsync(v => v.Total, ct);
    }
}
