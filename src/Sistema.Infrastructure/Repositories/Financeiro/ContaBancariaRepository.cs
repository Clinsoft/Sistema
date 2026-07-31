using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Repositories;

namespace Sistema.Infrastructure.Repositories.Financeiro;

public class ContaBancariaRepository(SistemaDbContext db)
    : BaseRepository<ContaBancaria>(db), IContaBancariaRepository
{
    public async Task<IEnumerable<ContaBancaria>> ListarAtivasAsync(Guid empresaId, CancellationToken ct)
        => await _db.ContasBancarias
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .OrderBy(c => c.Nome)
            .ToListAsync(ct);

    public async Task<ContaBancaria?> ObterPrincipalAsync(Guid empresaId, CancellationToken ct)
        => await _db.ContasBancarias
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.ContaPrincipal && c.Ativo, ct);

    public async Task<IEnumerable<MovimentacaoBancaria>> ListarMovimentacoesAsync(
        Guid empresaId, Guid contaId, DateTime inicio, DateTime fim, CancellationToken ct)
        => await _db.MovimentacoesBancarias
            .Where(m => m.EmpresaId == empresaId && m.ContaBancariaId == contaId
                && m.DataMovimentacao >= inicio && m.DataMovimentacao < fim.AddDays(1))
            .OrderBy(m => m.DataMovimentacao)
            .ToListAsync(ct);
}
