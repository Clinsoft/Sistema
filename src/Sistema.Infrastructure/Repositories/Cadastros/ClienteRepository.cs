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
    {
        // Busca por dígitos (telefone/CPF) ignorando máscara: normaliza a coluna
        // removendo ( ) - . espaço / e compara só os dígitos do termo.
        var digitos = SomenteDigitos(termo);
        var buscaDigitos = digitos.Length >= 3;

        return await _set.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo &&
                (string.IsNullOrEmpty(termo) ||
                 c.Nome.Contains(termo) ||
                 (c.CpfCnpj != null && c.CpfCnpj.Contains(termo)) ||
                 (c.Telefone != null && c.Telefone.Contains(termo)) ||
                 (c.Celular != null && c.Celular.Contains(termo)) ||
                 (buscaDigitos && c.CpfCnpj != null &&
                    c.CpfCnpj.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Replace(".", "").Replace("/", "").Contains(digitos)) ||
                 (buscaDigitos && c.Telefone != null &&
                    c.Telefone.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Replace(".", "").Replace("/", "").Contains(digitos)) ||
                 (buscaDigitos && c.Celular != null &&
                    c.Celular.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Replace(".", "").Replace("/", "").Contains(digitos))))
            .OrderBy(c => c.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
    }

    /// <summary>Só os dígitos do termo (roda em memória, sobre a string de busca).</summary>
    public static string SomenteDigitos(string? v)
        => string.IsNullOrEmpty(v) ? string.Empty : new string(v.Where(char.IsDigit).ToArray());

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
