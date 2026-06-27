using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Application.Compras.Commands;
using Sistema.Domain.Compras.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.API.Controllers.Compras;

[ApiController]
[Route("api/pedidos-compra")]
[Authorize]
public class PedidosCompraController(IMediator mediator, IPedidoCompraRepository repo, IUnitOfWork uow) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarPedidoCompraCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Ok(new { id });
    }

    [HttpPost("{id:guid}/enviar")]
    public async Task<IActionResult> Enviar(Guid id, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null || pedido.EmpresaId != empresaId) return NotFound();
        pedido.Enviar();
        repo.Atualizar(pedido);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/receber")]
    public async Task<IActionResult> Receber(Guid id, [FromBody] ReceberRequest req, CancellationToken ct)
    {
        await mediator.Send(new ReceberPedidoCompraCommand(id, req.LocalEstoqueId, req.UsuarioId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null || pedido.EmpresaId != empresaId) return NotFound();
        pedido.Cancelar();
        repo.Atualizar(pedido);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        CancellationToken ct)
    {
        var pedidos = await repo.ListarPorPeriodoAsync(empresaId, inicio, fim, ct);
        return Ok(pedidos.Select(p => new
        {
            p.Id, p.Numero, p.FornecedorId, p.Status,
            p.DataPedido, p.DataPrevisaoEntrega, p.DataRecebimento,
            p.Total, QtdItens = p.Itens.Count
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null) return NotFound();
        return Ok(new
        {
            pedido.Id, pedido.Numero, pedido.FornecedorId, pedido.Status,
            pedido.DataPedido, pedido.DataPrevisaoEntrega, pedido.Total,
            Itens = pedido.Itens.Select(i => new
            {
                i.Id, i.ProdutoId, i.Descricao,
                i.Quantidade, i.PrecoUnitario, i.Total
            })
        });
    }
}

public record ReceberRequest(Guid LocalEstoqueId, Guid UsuarioId);
