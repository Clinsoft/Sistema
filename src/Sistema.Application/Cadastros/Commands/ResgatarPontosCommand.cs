using FluentValidation;
using MediatR;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Cadastros.Commands;

/// <summary>Resgata pontos de fidelidade do cliente, retornando o valor de desconto em reais.</summary>
public record ResgatarPontosCommand(Guid EmpresaId, Guid ClienteId, int Pontos) : IRequest<decimal>;

public class ResgatarPontosValidator : AbstractValidator<ResgatarPontosCommand>
{
    public ResgatarPontosValidator()
    {
        RuleFor(x => x.ClienteId).NotEmpty();
        RuleFor(x => x.Pontos).GreaterThan(0);
    }
}

public class ResgatarPontosHandler(IClienteRepository repo, IUnitOfWork uow)
    : IRequestHandler<ResgatarPontosCommand, decimal>
{
    // 100 pontos = R$ 1,00
    private const decimal PontosPorReal = 100m;

    public async Task<decimal> Handle(ResgatarPontosCommand cmd, CancellationToken ct)
    {
        var cliente = await repo.ObterPorIdAsync(cmd.ClienteId, ct)
            ?? throw new KeyNotFoundException("Cliente não encontrado.");

        if (cliente.EmpresaId != cmd.EmpresaId) throw new UnauthorizedAccessException();
        if (cliente.PontosFidelidade < cmd.Pontos)
            throw new InvalidOperationException($"Saldo insuficiente. Disponível: {cliente.PontosFidelidade} pontos.");

        cliente.RetirarPontos(cmd.Pontos);
        repo.Atualizar(cliente);
        await uow.SalvarAsync(ct);

        return Math.Round(cmd.Pontos / PontosPorReal, 2);
    }
}
