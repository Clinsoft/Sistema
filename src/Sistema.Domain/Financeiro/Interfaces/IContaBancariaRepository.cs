using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Financeiro.Interfaces;

public interface IContaBancariaRepository : IRepository<ContaBancaria>
{
    Task<IEnumerable<ContaBancaria>> ListarAtivasAsync(Guid empresaId, CancellationToken ct);
    Task<ContaBancaria?> ObterPrincipalAsync(Guid empresaId, CancellationToken ct);
    Task<IEnumerable<MovimentacaoBancaria>> ListarMovimentacoesAsync(
        Guid empresaId, Guid contaId, DateTime inicio, DateTime fim, CancellationToken ct);
}
