using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Estoque.Interfaces;

public interface IProdutoRepository : IRepository<Produto>
{
    Task<Produto?> ObterPorCodigoAsync(Guid empresaId, string codigo, CancellationToken ct = default);

    /// <summary>Gera o próximo código interno numérico livre para a empresa (base 3001).</summary>
    Task<string> ProximoCodigoAsync(Guid empresaId, CancellationToken ct = default);
    Task<Produto?> ObterPorCodigoBarrasAsync(Guid empresaId, string codigoBarras, CancellationToken ct = default);
    Task<IReadOnlyList<Produto>> PesquisarAsync(Guid empresaId, string? termo, Guid? categoriaId, Guid? marcaId, bool? ativo, int pagina, int tamanhoPagina, CancellationToken ct = default);
    Task<int> ContarAsync(Guid empresaId, string? termo, Guid? categoriaId, bool? ativo, CancellationToken ct = default);
    Task<IReadOnlyList<Produto>> ListarEstoqueAbaixoMinimoAsync(Guid empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<Produto>> ListarComValidadeProximaAsync(Guid empresaId, int dias, CancellationToken ct = default);

    /// <summary>Retorna os nomes/siglas de unidades, categorias e marcas da empresa para enriquecer DTOs.</summary>
    Task<(IReadOnlyDictionary<Guid, string> Unidades,
          IReadOnlyDictionary<Guid, string> Categorias,
          IReadOnlyDictionary<Guid, string> Marcas)> ObterLookupsAsync(Guid empresaId, CancellationToken ct = default);
}
