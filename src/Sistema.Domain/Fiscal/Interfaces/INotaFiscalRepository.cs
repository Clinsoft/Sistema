using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Fiscal.Interfaces;

public interface INotaFiscalRepository : IRepository<NotaFiscal>
{
    Task<NotaFiscal?> ObterComItensAsync(Guid id, CancellationToken ct);
    Task<NotaFiscal?> ObterPorChaveAsync(string chaveAcesso, CancellationToken ct);
    Task<IEnumerable<NotaFiscal>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct);
    Task<long> ProximoNumeroAsync(Guid empresaId, ModeloNF modelo, CancellationToken ct);
}

public interface IConfiguracaoFiscalRepository : IRepository<ConfiguracaoFiscal>
{
    Task<ConfiguracaoFiscal?> ObterPorEmpresaAsync(Guid empresaId, CancellationToken ct);
}
