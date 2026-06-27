using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Repositories;

namespace Sistema.Infrastructure.Repositories.Fiscal;

public class NotaFiscalRepository(SistemaDbContext db)
    : BaseRepository<NotaFiscal>(db), INotaFiscalRepository
{
    public async Task<NotaFiscal?> ObterComItensAsync(Guid id, CancellationToken ct)
        => await _db.NotasFiscais.Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<NotaFiscal?> ObterPorChaveAsync(string chaveAcesso, CancellationToken ct)
        => await _db.NotasFiscais.FirstOrDefaultAsync(n => n.ChaveAcesso == chaveAcesso, ct);

    public async Task<IEnumerable<NotaFiscal>> ListarPorPeriodoAsync(
        Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct)
        => await _db.NotasFiscais
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao <= fim.AddDays(1))
            .OrderByDescending(n => n.DataEmissao)
            .ToListAsync(ct);

    public async Task<long> ProximoNumeroAsync(Guid empresaId, ModeloNF modelo, CancellationToken ct)
    {
        var max = await _db.NotasFiscais
            .Where(n => n.EmpresaId == empresaId && n.Modelo == modelo)
            .MaxAsync(n => (long?)n.Numero, ct);
        return (max ?? 0) + 1;
    }
}

public class ConfiguracaoFiscalRepository(SistemaDbContext db)
    : BaseRepository<ConfiguracaoFiscal>(db), IConfiguracaoFiscalRepository
{
    public async Task<ConfiguracaoFiscal?> ObterPorEmpresaAsync(Guid empresaId, CancellationToken ct)
        => await _db.ConfiguracoesFiscais.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
}
