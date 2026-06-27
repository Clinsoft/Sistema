using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Cadastros.Interfaces;

public interface IClienteRepository : IRepository<Cliente>
{
    Task<Cliente?> ObterPorCpfCnpjAsync(Guid empresaId, string cpfCnpj, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> PesquisarAsync(Guid empresaId, string termo, int pagina, int tamanhoPagina, CancellationToken ct = default);
    Task<int> ContarAtivosAsync(Guid empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<Cliente>> ListarAniversariantesAsync(Guid empresaId, int mes, CancellationToken ct = default);
}
