using FluentValidation;
using MediatR;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Compras.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Compras.Commands;

public record ItemPedidoCompraDto(Guid ProdutoId, string Descricao, decimal Quantidade, decimal PrecoUnitario);

public record CriarPedidoCompraCommand(
    Guid EmpresaId, Guid FornecedorId, Guid UsuarioId,
    IList<ItemPedidoCompraDto> Itens,
    DateTime? PrevisaoEntrega = null,
    string? Observacao = null) : IRequest<Guid>;

public class CriarPedidoCompraValidator : AbstractValidator<CriarPedidoCompraCommand>
{
    public CriarPedidoCompraValidator()
    {
        RuleFor(x => x.FornecedorId).NotEmpty();
        RuleFor(x => x.Itens).NotEmpty().WithMessage("O pedido deve ter ao menos um item.");
        RuleForEach(x => x.Itens).ChildRules(i =>
        {
            i.RuleFor(x => x.Quantidade).GreaterThan(0);
            i.RuleFor(x => x.PrecoUnitario).GreaterThanOrEqualTo(0);
            i.RuleFor(x => x.Descricao).NotEmpty();
        });
    }
}

public class CriarPedidoCompraHandler(IPedidoCompraRepository repo, IUnitOfWork uow)
    : IRequestHandler<CriarPedidoCompraCommand, Guid>
{
    public async Task<Guid> Handle(CriarPedidoCompraCommand cmd, CancellationToken ct)
    {
        var numero = await repo.ProximoNumeroAsync(cmd.EmpresaId, ct);
        var pedido = PedidoCompra.Criar(cmd.EmpresaId, cmd.FornecedorId, cmd.UsuarioId, numero, cmd.PrevisaoEntrega);

        foreach (var item in cmd.Itens)
            pedido.AdicionarItem(item.ProdutoId, item.Descricao, item.Quantidade, item.PrecoUnitario);

        await repo.AdicionarAsync(pedido, ct);
        await uow.SalvarAsync(ct);
        return pedido.Id;
    }
}
