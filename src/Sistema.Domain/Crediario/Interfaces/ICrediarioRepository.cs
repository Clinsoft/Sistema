using CrediarioEntity = Sistema.Domain.Crediario.Entities.Crediario;
using Sistema.Domain.Crediario.Entities;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Domain.Crediario.Interfaces;

public interface ICrediarioRepository : IRepository<CrediarioEntity>
{
    Task<string> ProximoNumeroAsync(Guid empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<CrediarioEntity>> ListarPorClienteAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default);
    Task<CrediarioEntity?> ObterComParcelasAsync(Guid id, CancellationToken ct = default);
}

public interface IParcelaCrediarioRepository : IRepository<ParcelaCrediario>
{
    Task<ParcelaCrediario?> ObterComCrediarioAsync(Guid parcelaId, CancellationToken ct = default);
}
