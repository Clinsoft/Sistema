using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.Domain.Vendas.Interfaces;

public interface IPDVSessaoRepository : IRepository<PDVSessao>
{
    Task<PDVSessao?> ObterSessaoAbertaAsync(Guid empresaId, Guid usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<PDVSessao>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default);
}
