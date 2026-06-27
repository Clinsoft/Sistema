using FluentValidation;
using MediatR;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Commands;

public record AdicionarItemVendaCommand(
    Guid VendaId, Guid ProdutoId,
    decimal Quantidade, decimal? PrecoUnitario = null,
    decimal PercentualDesconto = 0) : IRequest;

public class AdicionarItemVendaValidator : AbstractValidator<AdicionarItemVendaCommand>
{
    public AdicionarItemVendaValidator()
    {
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.PercentualDesconto).InclusiveBetween(0, 100);
    }
}

public class AdicionarItemVendaHandler(IVendaRepository vendaRepo, IProdutoRepository produtoRepo, IUnitOfWork uow)
    : IRequestHandler<AdicionarItemVendaCommand>
{
    public async Task Handle(AdicionarItemVendaCommand cmd, CancellationToken ct)
    {
        var venda = await vendaRepo.ObterComItensAsync(cmd.VendaId, ct)
            ?? throw new KeyNotFoundException("Venda não encontrada.");

        var produto = await produtoRepo.ObterPorIdAsync(cmd.ProdutoId, ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        var preco = cmd.PrecoUnitario ?? produto.PrecoVenda;
        venda.AdicionarItem(produto.Id, produto.Descricao, cmd.Quantidade, preco, cmd.PercentualDesconto);

        vendaRepo.Atualizar(venda);
        await uow.SalvarAsync(ct);
    }
}
