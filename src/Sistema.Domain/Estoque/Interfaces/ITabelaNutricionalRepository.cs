using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Estoque.Interfaces;

public interface ITabelaNutricionalRepository : IRepository<TabelaNutricional>
{
    Task<TabelaNutricional?> ObterPorProdutoAsync(Guid produtoId, CancellationToken ct = default);
}

public interface IReceitaProdutoRepository : IRepository<ReceitaProduto>
{
    Task<IReadOnlyList<ReceitaProduto>> ListarPorProdutoAsync(Guid produtoId, CancellationToken ct = default);
}

public interface ISugestaoProdutoRepository : IRepository<SugestaoProduto>
{
    Task<IReadOnlyList<SugestaoProduto>> ListarPorProdutoAsync(Guid produtoId, CancellationToken ct = default);
}
