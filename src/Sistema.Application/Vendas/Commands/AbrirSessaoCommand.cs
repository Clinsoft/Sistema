using FluentValidation;
using MediatR;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Commands;

public record AbrirSessaoCommand(Guid EmpresaId, Guid UsuarioId, Guid LocalEstoqueId, decimal SaldoAbertura)
    : IRequest<Guid>;

public class AbrirSessaoValidator : AbstractValidator<AbrirSessaoCommand>
{
    public AbrirSessaoValidator()
    {
        RuleFor(x => x.SaldoAbertura).GreaterThanOrEqualTo(0);
    }
}

public class AbrirSessaoHandler(IPDVSessaoRepository repo, IUnitOfWork uow)
    : IRequestHandler<AbrirSessaoCommand, Guid>
{
    public async Task<Guid> Handle(AbrirSessaoCommand cmd, CancellationToken ct)
    {
        var aberta = await repo.ObterSessaoAbertaAsync(cmd.EmpresaId, cmd.UsuarioId, ct);
        if (aberta is not null)
            throw new InvalidOperationException("Usuário já possui uma sessão de caixa aberta.");

        var sessao = PDVSessao.Abrir(cmd.EmpresaId, cmd.UsuarioId, cmd.LocalEstoqueId, cmd.SaldoAbertura);
        await repo.AdicionarAsync(sessao, ct);
        await uow.SalvarAsync(ct);
        return sessao.Id;
    }
}
