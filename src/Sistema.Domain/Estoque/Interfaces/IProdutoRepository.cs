using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Estoque.Interfaces;

public interface IProdutoRepository : IRepository<Produto>
{
    Task<Produto?> ObterPorCodigoAsync(Guid empresaId, string codigo, CancellationToken ct = default);
    Task<Produto?> ObterPorCodigoBarrasAsync(Guid empresaId, string codigoBarras, CancellationToken ct = default);
    Task<IReadOnlyList<Produto>> PesquisarAsync(Guid empresaId, string? termo, Guid? categoriaId, Guid? marcaId, bool? ativo, int pagina, int tamanhoPagina, CancellationToken ct = default);
    Task<int> ContarAsync(Guid empresaId, string? termo, Guid? categoriaId, bool? ativo, CancellationToken ct = default);
    Task<IReadOnlyList<Produto>> ListarEstoqueAbaixoMinimoAsync(Guid empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<Produto>> ListarComValidadeProximaAsync(Guid empresaId, int dias, CancellationToken ct = default);
}
