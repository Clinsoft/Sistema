using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Cadastros;

public class FornecedorRepository(SistemaDbContext db) : BaseRepository<Fornecedor>(db), IFornecedorRepository
{
    public async Task<IReadOnlyList<Fornecedor>> PesquisarAsync(Guid empresaId, string? termo, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId && f.Ativo &&
                (string.IsNullOrEmpty(termo) ||
                 f.RazaoSocial.Contains(termo) ||
                 (f.NomeFantasia != null && f.NomeFantasia.Contains(termo)) ||
                 f.Cnpj.Contains(termo)))
            .OrderBy(f => f.RazaoSocial)
            .ToListAsync(ct);
}
