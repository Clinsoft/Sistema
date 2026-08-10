using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Repositories;

namespace Sistema.Infrastructure.Repositories.Financeiro;

public class LancamentoFinanceiroRepository(SistemaDbContext db)
    : BaseRepository<LancamentoFinanceiro>(db), ILancamentoFinanceiroRepository
{
    public async Task<IEnumerable<LancamentoFinanceiro>> ListarPorPeriodoAsync(
        Guid empresaId, TipoLancamento tipo, DateTime inicio, DateTime fim, CancellationToken ct)
        => await _db.LancamentosFinanceiros
            .Where(l => l.EmpresaId == empresaId && l.Tipo == tipo
                && l.DataVencimento >= inicio.Date && l.DataVencimento < fim.Date.AddDays(1))
            .OrderBy(l => l.DataVencimento)
            .ToListAsync(ct);

    public async Task<IEnumerable<LancamentoFinanceiro>> ListarVencidosAsync(
        Guid empresaId, TipoLancamento tipo, CancellationToken ct)
        => await _db.LancamentosFinanceiros
            .Where(l => l.EmpresaId == empresaId && l.Tipo == tipo
                && l.Status == StatusLancamento.EmAberto && l.DataVencimento < DateTime.Today)
            .OrderBy(l => l.DataVencimento)
            .ToListAsync(ct);

    public async Task<IEnumerable<LancamentoFinanceiro>> ListarPorClienteAsync(
        Guid empresaId, Guid clienteId, CancellationToken ct)
        => await _db.LancamentosFinanceiros
            .Where(l => l.EmpresaId == empresaId && l.ClienteId == clienteId)
            .OrderByDescending(l => l.DataVencimento)
            .ToListAsync(ct);

    public async Task<IEnumerable<LancamentoFinanceiro>> ListarPorFornecedorAsync(
        Guid empresaId, Guid fornecedorId, CancellationToken ct)
        => await _db.LancamentosFinanceiros
            .Where(l => l.EmpresaId == empresaId && l.FornecedorId == fornecedorId)
            .OrderByDescending(l => l.DataVencimento)
            .ToListAsync(ct);

    public async Task<decimal> TotalEmAbertoAsync(Guid empresaId, TipoLancamento tipo, CancellationToken ct)
        => await _db.LancamentosFinanceiros
            .Where(l => l.EmpresaId == empresaId && l.Tipo == tipo
                && l.Status == StatusLancamento.EmAberto)
            .SumAsync(l => l.ValorOriginal - l.ValorPago, ct);
}
