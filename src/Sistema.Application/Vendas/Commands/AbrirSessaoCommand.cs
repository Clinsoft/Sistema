using FluentValidation;
using MediatR;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Commands;

public record AbrirSessaoCommand(Guid EmpresaId, Guid UsuarioId, Guid LocalEstoqueId, decimal SaldoAbertura,
    bool Forcar = false) : IRequest<Guid>;

public class AbrirSessaoValidator : AbstractValidator<AbrirSessaoCommand>
{
    public AbrirSessaoValidator()
    {
        RuleFor(x => x.SaldoAbertura).GreaterThanOrEqualTo(0);
    }
}

public class AbrirSessaoHandler(IPDVSessaoRepository repo, IVendaRepository vendaRepo, IUnitOfWork uow)
    : IRequestHandler<AbrirSessaoCommand, Guid>
{
    public async Task<Guid> Handle(AbrirSessaoCommand cmd, CancellationToken ct)
    {
        // Se o operador deixou um caixa aberto (ex.: esqueceu de fechar no dia anterior),
        // fecha automaticamente com o saldo ESPERADO calculado pelo sistema antes de abrir o novo.
        var aberta = await repo.ObterSessaoAbertaAsync(cmd.EmpresaId, cmd.UsuarioId, ct);
        if (aberta is not null)
        {
            var (dinheiro, troco) = await vendaRepo.TotaisDinheiroAsync(
                aberta.EmpresaId, aberta.Abertura, DateTime.Now, aberta.UsuarioId, aberta.LocalEstoqueId, ct);
            var saldoEsperado = Math.Round(
                aberta.SaldoAbertura + (dinheiro - troco) + aberta.TotalSuprimentos - aberta.TotalSangrias, 2);
            aberta.Fechar(saldoEsperado,
                $"Fechamento automático — caixa deixado aberto desde {aberta.Abertura:dd/MM/yyyy HH:mm} (fechado ao reabrir).");
            repo.Atualizar(aberta);
        }

        var sessao = PDVSessao.Abrir(cmd.EmpresaId, cmd.UsuarioId, cmd.LocalEstoqueId, cmd.SaldoAbertura);
        await repo.AdicionarAsync(sessao, ct);
        await uow.SalvarAsync(ct);
        return sessao.Id;
    }
}
