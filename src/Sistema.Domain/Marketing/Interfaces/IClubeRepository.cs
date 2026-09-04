using Sistema.Domain.Marketing.Entities;

namespace Sistema.Domain.Marketing.Interfaces;

public interface IClubeRepository
{
    /// <summary>Membro do clube do cliente na empresa (rastreado), ou null se ainda não é membro.</summary>
    Task<MembroClube?> ObterMembroAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default);
    Task AdicionarMembroAsync(MembroClube membro, CancellationToken ct = default);

    /// <summary>Configuração do clube da empresa (regras de cashback), ou null se não configurada.</summary>
    Task<ConfiguracaoClube?> ObterConfiguracaoAsync(Guid empresaId, CancellationToken ct = default);
    Task AdicionarMovimentoAsync(MovimentoCashback movimento, CancellationToken ct = default);
}
