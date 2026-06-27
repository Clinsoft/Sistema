using FluentValidation;
using MediatR;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Commands;

public record ItemDevolucaoDto(Guid ProdutoId, string Descricao, decimal Quantidade, decimal ValorUnitario);

public record RegistrarDevolucaoCommand(
    Guid EmpresaId,
    Guid VendaId,
    string Motivo,
    List<ItemDevolucaoDto> Itens,
    bool ReporEstoque = true) : IRequest<Guid>;

public class RegistrarDevolucaoValidator : AbstractValidator<RegistrarDevolucaoCommand>
{
    public RegistrarDevolucaoValidator()
    {
        RuleFor(x => x.VendaId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Itens).NotEmpty().WithMessage("Informe pelo menos um item para devolução.");
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.ProdutoId).NotEmpty();
            item.RuleFor(i => i.Quantidade).GreaterThan(0);
            item.RuleFor(i => i.ValorUnitario).GreaterThanOrEqualTo(0);
        });
    }
}

public class RegistrarDevolucaoHandler(
    IVendaRepository vendaRepo,
    IDevolucaoVendaRepository devolucaoRepo,
    IProdutoRepository produtoRepo,
    IMovimentacaoEstoqueRepository movRepo,
    IUnitOfWork uow)
    : IRequestHandler<RegistrarDevolucaoCommand, Guid>
{
    public async Task<Guid> Handle(RegistrarDevolucaoCommand cmd, CancellationToken ct)
    {
        var venda = await vendaRepo.ObterComItensAsync(cmd.VendaId, ct)
            ?? throw new KeyNotFoundException("Venda não encontrada.");

        if (venda.EmpresaId != cmd.EmpresaId)
            throw new UnauthorizedAccessException();

        if (venda.Status != StatusVenda.Finalizada)
            throw new InvalidOperationException("Somente vendas finalizadas podem ser devolvidas.");

        var devolucao = DevolucaoVenda.Criar(
            cmd.EmpresaId, cmd.VendaId, venda.Numero, venda.ClienteId,
            cmd.Motivo,
            cmd.Itens.Select(i => (i.ProdutoId, i.Descricao, i.Quantidade, i.ValorUnitario)));

        await devolucaoRepo.AdicionarAsync(devolucao, ct);

        if (cmd.ReporEstoque)
        {
            foreach (var item in cmd.Itens)
            {
                var produto = await produtoRepo.ObterPorIdAsync(item.ProdutoId, ct);

                var mov = MovimentacaoEstoque.Criar(
                    cmd.EmpresaId, item.ProdutoId, venda.LocalEstoqueId,
                    TipoMovimentacao.Devolucao, item.Quantidade,
                    item.ValorUnitario,
                    documentoOrigem: $"DEV-{venda.Numero}");

                await movRepo.AdicionarAsync(mov, ct);

                if (produto is not null)
                {
                    produto.AjustarEstoque(item.Quantidade);
                    produtoRepo.Atualizar(produto);
                }
            }
        }

        await uow.SalvarAsync(ct);
        return devolucao.Id;
    }
}
