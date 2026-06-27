using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Repositories.Cadastros;

public class ClienteRepository(SistemaDbContext db) : BaseRepository<Cliente>(db), IClienteRepository
{
    public async Task<Cliente?> ObterPorCpfCnpjAsync(Guid empresaId, string cpfCnpj, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.CpfCnpj == cpfCnpj, ct);

    public async Task<IReadOnlyList<Cliente>> PesquisarAsync(Guid empresaId, string termo, int pagina, int tamanhoPagina, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo &&
                (string.IsNullOrEmpty(termo) ||
                 c.Nome.Contains(termo) ||
                 (c.CpfCnpj != null && c.CpfCnpj.Contains(termo)) ||
                 (c.Telefone != null && c.Telefone.Contains(termo)) ||
                 (c.Celular != null && c.Celular.Contains(termo))))
            .OrderBy(c => c.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

    public async Task<int> ContarAtivosAsync(Guid empresaId, CancellationToken ct = default)
        => await _set.CountAsync(c => c.EmpresaId == empresaId && c.Ativo, ct);

    public async Task<IReadOnlyList<Cliente>> ListarAniversariantesAsync(Guid empresaId, int mes, CancellationToken ct = default)
        => await _set.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo &&
                c.DataNascimento.HasValue &&
                c.DataNascimento.Value.Month == mes)
            .OrderBy(c => c.DataNascimento!.Value.Day)
            .ToListAsync(ct);
}
