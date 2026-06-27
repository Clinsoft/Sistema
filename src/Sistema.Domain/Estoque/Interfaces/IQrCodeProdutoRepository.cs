using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Estoque.Interfaces;

public interface IQrCodeProdutoRepository : IRepository<QrCodeProduto>
{
    Task<QrCodeProduto?> ObterPorSlugAsync(string slug, CancellationToken ct = default);
    Task<QrCodeProduto?> ObterPorProdutoAsync(Guid produtoId, CancellationToken ct = default);
}
