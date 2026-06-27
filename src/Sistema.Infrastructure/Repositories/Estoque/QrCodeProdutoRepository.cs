using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Estoque;

public class QrCodeProdutoRepository(SistemaDbContext db) : BaseRepository<QrCodeProduto>(db), IQrCodeProdutoRepository
{
    public async Task<QrCodeProduto?> ObterPorSlugAsync(string slug, CancellationToken ct = default)
        => await _set.AsNoTracking().FirstOrDefaultAsync(q => q.Slug == slug, ct);

    public async Task<QrCodeProduto?> ObterPorProdutoAsync(Guid produtoId, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(q => q.ProdutoId == produtoId, ct);
}
