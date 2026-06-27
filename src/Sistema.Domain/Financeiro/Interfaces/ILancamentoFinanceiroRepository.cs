using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Financeiro.Interfaces;

public interface ILancamentoFinanceiroRepository : IRepository<LancamentoFinanceiro>
{
    Task<IEnumerable<LancamentoFinanceiro>> ListarPorPeriodoAsync(
        Guid empresaId, TipoLancamento tipo, DateTime inicio, DateTime fim, CancellationToken ct);

    Task<IEnumerable<LancamentoFinanceiro>> ListarVencidosAsync(
        Guid empresaId, TipoLancamento tipo, CancellationToken ct);

    Task<IEnumerable<LancamentoFinanceiro>> ListarPorClienteAsync(
        Guid empresaId, Guid clienteId, CancellationToken ct);

    Task<IEnumerable<LancamentoFinanceiro>> ListarPorFornecedorAsync(
        Guid empresaId, Guid fornecedorId, CancellationToken ct);

    Task<decimal> TotalEmAbertoAsync(Guid empresaId, TipoLancamento tipo, CancellationToken ct);
}
