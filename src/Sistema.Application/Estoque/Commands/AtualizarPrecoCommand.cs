using FluentValidation;
using MediatR;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Estoque.Commands;

public record AtualizarPrecoCommand(Guid EmpresaId, Guid ProdutoId, decimal NovoCusto, decimal NovoPreco) : IRequest;

public class AtualizarPrecoValidator : AbstractValidator<AtualizarPrecoCommand>
{
    public AtualizarPrecoValidator()
    {
        RuleFor(x => x.NovoCusto).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NovoPreco).GreaterThan(0);
    }
}

public class AtualizarPrecoHandler(IProdutoRepository repo, IUnitOfWork uow)
    : IRequestHandler<AtualizarPrecoCommand>
{
    public async Task Handle(AtualizarPrecoCommand cmd, CancellationToken ct)
    {
        var produto = await repo.ObterPorIdAsync(cmd.ProdutoId, ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        produto.AtualizarPreco(cmd.NovoCusto, cmd.NovoPreco);
        repo.Atualizar(produto);
        await uow.SalvarAsync(ct);
    }
}
