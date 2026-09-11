using MediatR;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Compras.Interfaces;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Compras.Commands;

/// <summary>Quantidade efetivamente recebida de um item da OC (confronto do recebimento manual).</summary>
public record ItemRecebido(Guid ProdutoId, decimal Quantidade);

/// <summary>
/// Recebe um pedido de compra manualmente (sem NF-e). <paramref name="Itens"/> traz a quantidade
/// RECEBIDA de cada item da OC. O que foi pedido e não veio (faltou) vira um rascunho com o mesmo
/// fornecedor, para re-pedir depois.
/// </summary>
public record ReceberPedidoCompraCommand(
    Guid PedidoId, Guid LocalEstoqueId, Guid UsuarioId,
    List<ItemRecebido>? Itens = null) : IRequest<ReceberPedidoCompraResult>;

public record ReceberPedidoCompraResult(string? RascunhoNumero, int Faltantes);

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

        // Quantidade recebida por produto (sem lista = recebe tudo o que foi pedido).
        var recebidoPorProduto = cmd.Itens is { Count: > 0 }
            ? cmd.Itens.GroupBy(i => i.ProdutoId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantidade))
            : pedido.Itens.ToDictionary(i => i.ProdutoId, i => i.Quantidade);

        // Faltantes: item da OC cuja quantidade recebida ficou abaixo da pedida.
        var faltantes = new List<(Guid ProdutoId, string Descricao, decimal Qtd, decimal Preco)>();

        foreach (var item in pedido.Itens)
        {
            var recebida = recebidoPorProduto.TryGetValue(item.ProdutoId, out var q) ? q : item.Quantidade;
            if (recebida < 0) recebida = 0;

            // Entra no estoque o que realmente chegou.
            if (recebida > 0)
            {
                var produto = await produtoRepo.ObterPorIdAsync(item.ProdutoId, ct);
                if (produto is not null)
                {
                    var mov = MovimentacaoEstoque.Criar(
                        pedido.EmpresaId, item.ProdutoId, cmd.LocalEstoqueId,
                        TipoMovimentacao.Entrada, recebida, item.PrecoUnitario,
                        documentoOrigem: pedido.Numero, usuarioId: cmd.UsuarioId);
                    produto.AjustarEstoque(recebida);
                    await movRepo.AdicionarAsync(mov, ct);
                    produtoRepo.Atualizar(produto);
                }
            }

            var faltou = item.Quantidade - recebida;
            if (faltou > 0)
                faltantes.Add((item.ProdutoId, item.Descricao, faltou, item.PrecoUnitario));
        }

        pedido.ReceberComNota("Recebimento manual (sem NF-e)");
        pedidoRepo.Atualizar(pedido);

        // Faltou algo → rascunho com o mesmo fornecedor para re-pedir.
        string? rascunhoNumero = null;
        if (faltantes.Count > 0)
        {
            rascunhoNumero = await pedidoRepo.ProximoNumeroAsync(pedido.EmpresaId, ct);
            var rascunho = PedidoCompra.Criar(
                pedido.EmpresaId, pedido.FornecedorId, cmd.UsuarioId, rascunhoNumero,
                localEstoqueId: pedido.LocalEstoqueId ?? cmd.LocalEstoqueId,
                requisicaoCompraId: pedido.RequisicaoCompraId);
            foreach (var f in faltantes)
                rascunho.AdicionarItem(f.ProdutoId, f.Descricao, f.Qtd, f.Preco);
            rascunho.DefinirObservacao($"Itens da OC {pedido.Numero} que faltaram no recebimento — re-pedir ao fornecedor.");
            await pedidoRepo.AdicionarAsync(rascunho, ct);
        }

        await uow.SalvarAsync(ct);
        return new ReceberPedidoCompraResult(rascunhoNumero, faltantes.Count);
    }
}
