#pragma warning disable CS9107
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Estoque;

public class ProdutoRepository(SistemaDbContext db) : BaseRepository<Produto>(db), IProdutoRepository
{
    public async Task<Produto?> ObterPorCodigoAsync(Guid empresaId, string codigo, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmpresaId == empresaId && p.Codigo == codigo, ct);

    public async Task<Produto?> ObterPorCodigoBarrasAsync(Guid empresaId, string codigoBarras, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmpresaId == empresaId && p.CodigoBarras == codigoBarras, ct);

    public async Task<IReadOnlyList<Produto>> PesquisarAsync(Guid empresaId, string? termo,
        Guid? categoriaId, Guid? marcaId, bool? ativo, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(p => p.EmpresaId == empresaId);

        if (!string.IsNullOrEmpty(termo))
            query = query.Where(p =>
                p.Descricao.Contains(termo) ||
                p.Codigo.Contains(termo) ||
                (p.CodigoBarras != null && p.CodigoBarras.Contains(termo)));

        if (categoriaId.HasValue) query = query.Where(p => p.CategoriaId == categoriaId);
        if (marcaId.HasValue) query = query.Where(p => p.MarcaId == marcaId);
        if (ativo.HasValue) query = query.Where(p => p.Ativo == ativo);

        return await query
            .OrderBy(p => p.Descricao)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyDictionary<Guid, string> Unidades,
                       IReadOnlyDictionary<Guid, string> Categorias,
                       IReadOnlyDictionary<Guid, string> Marcas)> ObterLookupsAsync(Guid empresaId, CancellationToken ct = default)
    {
        var unidades = await _db.UnidadesMedida.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId)
            .ToDictionaryAsync(u => u.Id, u => u.Sigla, ct);
        var categorias = await _db.Categorias.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId)
            .ToDictionaryAsync(c => c.Id, c => c.Nome, ct);
        var marcas = await _db.Marcas.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId)
            .ToDictionaryAsync(m => m.Id, m => m.Nome, ct);
        return (unidades, categorias, marcas);
    }

    public async Task<int> ContarAsync(Guid empresaId, string? termo, Guid? categoriaId, bool? ativo, CancellationToken ct = default)
    {
        var query = _set.Where(p => p.EmpresaId == empresaId);
        if (!string.IsNullOrEmpty(termo))
            query = query.Where(p => p.Descricao.Contains(termo) || p.Codigo.Contains(termo));
        if (categoriaId.HasValue) query = query.Where(p => p.CategoriaId == categoriaId);
        if (ativo.HasValue) query = query.Where(p => p.Ativo == ativo);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Produto>> ListarEstoqueAbaixoMinimoAsync(Guid empresaId, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo && p.EstoqueAtual <= p.EstoqueMinimo)
            .OrderBy(p => p.Descricao)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Produto>> ListarComValidadeProximaAsync(Guid empresaId, int dias, CancellationToken ct = default)
    {
        var limite = DateTime.Today.AddDays(dias);
        return await _db.Lotes.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.DataValidade.HasValue && l.DataValidade <= limite && l.Quantidade > 0)
            .Join(_db.Produtos, l => l.ProdutoId, p => p.Id, (_, p) => p)
            .Distinct()
            .OrderBy(p => p.Descricao)
            .ToListAsync(ct);
    }
}
