using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Estoque.Interfaces;

public interface IMovimentacaoEstoqueRepository : IRepository<MovimentacaoEstoque>
{
    Task<IReadOnlyList<MovimentacaoEstoque>> ListarPorProdutoAsync(Guid empresaId, Guid produtoId, CancellationToken ct = default);
    Task<IReadOnlyList<MovimentacaoEstoque>> ListarPorPeriodoAsync(Guid empresaId, DateTime inicio, DateTime fim, CancellationToken ct = default);
}
