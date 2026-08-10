using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.WhatsApp;

/// <summary>
/// Pedidos recebidos pela loja (WhatsApp e VITRINE pública). A tela de Pedidos do
/// front consome estas rotas (antes apontavam para endpoints inexistentes).
/// </summary>
[ApiController]
[Route("api/whatsapp/pedidos")]
[Authorize]
public class PedidosWhatsAppController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    private static readonly TimeZoneInfo TzBrasil =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Lista pedidos da loja (filtra por loja quando informado).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] Guid? localEstoqueId, [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.PedidosWhatsApp.AsNoTracking()
            .Include(p => p.Itens)
            .Where(p => p.EmpresaId == empresaId
                     && (localEstoqueId == null || p.LocalEstoqueId == localEstoqueId));

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusPedidoWhatsApp>(status, out var st))
            query = query.Where(p => p.Status == st);

        var pedidos = await query.OrderByDescending(p => p.CriadoEm).ToListAsync(ct);

        return Ok(pedidos.Select(p => new
        {
            p.Id,
            data = TimeZoneInfo.ConvertTimeFromUtc(p.CriadoEm, TzBrasil).ToString("dd/MM/yyyy HH:mm"),
            clienteNome = p.NomeCliente,
            clienteTelefone = p.TelefoneCliente,
            p.Total,
            status = p.Status.ToString(),
            tipoEntrega = p.TipoEntrega.ToString(),
            p.EnderecoEntrega,
            p.Observacao,
            itens = p.Itens.Select(i => new { i.Descricao, i.Quantidade, i.PrecoUnitario, i.Total }),
        }));
    }

    /// <summary>Avança/atualiza o status do pedido (Novo → EmSeparacao → Enviado → Entregue).</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id,
        [FromBody] AtualizarStatusPedidoRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<StatusPedidoWhatsApp>(req.Status, out var novo))
            return BadRequest(new { mensagem = "Status inválido." });

        var pedido = await db.PedidosWhatsApp.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pedido is null) return NotFound(new { mensagem = "Pedido não encontrado." });

        pedido.AvancarStatus(novo);
        await uow.SalvarAsync(ct);
        return Ok(new { pedido.Id, status = pedido.Status.ToString() });
    }
}

public record AtualizarStatusPedidoRequest(string Status);
