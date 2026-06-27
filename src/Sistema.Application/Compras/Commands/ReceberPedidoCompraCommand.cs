using MediatR;
using Sistema.Domain.Compras.Interfaces;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Compras.Commands;

public record ReceberPedidoCompraCommand(
    Guid PedidoId, Guid LocalEstoqueId, Guid UsuarioId) : IRequest;

public class ReceberPedidoCompraHandler(
    IPedidoCompraRepository pedidoRepo,
    IProdutoRepository produtoRepo,
    IMovimentacaoEstoqueRepository movRepo,
    IUnitOfWork uow)
    : IRequestHandler<ReceberPedidoCompraCommand>
{
    public async Task Handle(ReceberPedidoCompraCommand cmd, CancellationToken ct)
    {
        var pedido = await pedidoRepo.ObterComItensAsync(cmd.PedidoId, ct)
            ?? throw new KeyNotFoundException("Pedido de compra não encontrado.");

        if (pedido.Status != Domain.Compras.Entities.StatusPedidoCompra.Enviado)
            throw new InvalidOperationException("Apenas pedidos com status 'Enviado' podem ser recebidos.");

        foreach (var item in pedido.Itens)
        {
            var produto = await produtoRepo.ObterPorIdAsync(item.ProdutoId, ct);
            if (produto is null) continue;

            var mov = MovimentacaoEstoque.Criar(
                pedido.EmpresaId, item.ProdutoId, cmd.LocalEstoqueId,
                TipoMovimentacao.Entrada, item.Quantidade, item.PrecoUnitario,
                documentoOrigem: pedido.Numero, usuarioId: cmd.UsuarioId);

            produto.AjustarEstoque(item.Quantidade);
            await movRepo.AdicionarAsync(mov, ct);
            produtoRepo.Atualizar(produto);
        }

        pedido.Receber();
        pedidoRepo.Atualizar(pedido);
        await uow.SalvarAsync(ct);
    }
}
