using MediatR;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Compras.Interfaces;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Compras.Commands;

/// <summary>Item efetivamente recebido no balcão (confronto do recebimento manual).</summary>
public record ItemRecebido(Guid ProdutoId, string Descricao, decimal Quantidade, decimal PrecoUnitario);

/// <summary>
/// Recebe um pedido de compra manualmente (sem NF-e). Quando <paramref name="Itens"/> é informado,
/// confronta o que chegou com a OC: itens que NÃO estavam na OC entram no estoque e viram um rascunho
/// de pedido, para o comprador regularizar depois.
/// </summary>
public record ReceberPedidoCompraCommand(
    Guid PedidoId, Guid LocalEstoqueId, Guid UsuarioId,
    List<ItemRecebido>? Itens = null) : IRequest<ReceberPedidoCompraResult>;

public record ReceberPedidoCompraResult(string? RascunhoNumero, int Divergentes);

public class ReceberPedidoCompraHandler(
    IPedidoCompraRepository pedidoRepo,
    IProdutoRepository produtoRepo,
    IMovimentacaoEstoqueRepository movRepo,
    IUnitOfWork uow)
    : IRequestHandler<ReceberPedidoCompraCommand, ReceberPedidoCompraResult>
{
    public async Task<ReceberPedidoCompraResult> Handle(ReceberPedidoCompraCommand cmd, CancellationToken ct)
    {
        var pedido = await pedidoRepo.ObterComItensAsync(cmd.PedidoId, ct)
            ?? throw new KeyNotFoundException("Pedido de compra não encontrado.");

        if (pedido.Status != StatusPedidoCompra.Enviado)
            throw new InvalidOperationException("Apenas pedidos com status 'Enviado' podem ser recebidos.");

        // Base: o que foi recebido. Sem lista informada, recebe a OC como está (compatível).
        var recebidos = cmd.Itens is { Count: > 0 }
            ? cmd.Itens
            : pedido.Itens.Select(i => new ItemRecebido(i.ProdutoId, i.Descricao, i.Quantidade, i.PrecoUnitario)).ToList();

        foreach (var item in recebidos)
        {
            if (item.Quantidade <= 0) continue;
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

        pedido.ReceberComNota("Recebimento manual (sem NF-e)");
        pedidoRepo.Atualizar(pedido);

        // Confronto: recebidos que NÃO estavam na OC viram um rascunho para regularizar.
        string? rascunhoNumero = null;
        var divergentes = 0;
        var naOc = pedido.Itens.Select(i => i.ProdutoId).ToHashSet();
        var extras = recebidos.Where(r => r.Quantidade > 0 && !naOc.Contains(r.ProdutoId)).ToList();
        if (extras.Count > 0)
        {
            rascunhoNumero = await pedidoRepo.ProximoNumeroAsync(pedido.EmpresaId, ct);
            var rascunho = PedidoCompra.Criar(
                pedido.EmpresaId, pedido.FornecedorId, cmd.UsuarioId, rascunhoNumero,
                localEstoqueId: cmd.LocalEstoqueId);
            foreach (var e in extras)
                rascunho.AdicionarItem(e.ProdutoId, e.Descricao, e.Quantidade, e.PrecoUnitario);
            rascunho.DefinirObservacao($"Itens recebidos no balcão que não estavam na OC {pedido.Numero}.");
            await pedidoRepo.AdicionarAsync(rascunho, ct);
            divergentes = extras.Count;
        }

        await uow.SalvarAsync(ct);
        return new ReceberPedidoCompraResult(rascunhoNumero, divergentes);
    }
}
