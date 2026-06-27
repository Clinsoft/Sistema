using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Vendas.Commands;
using Sistema.Application.Vendas.Queries;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Vendas;

[ApiController]
[Route("api/vendas")]
[Authorize]
public class VendasController(IMediator mediator, SistemaDbContext db) : ControllerBase
{
    /// <summary>Inicia uma nova venda no PDV.</summary>
    [HttpPost("iniciar")]
    public async Task<IActionResult> Iniciar([FromBody] IniciarVendaCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Ok(new { id });
    }

    /// <summary>Adiciona um item à venda aberta.</summary>
    [HttpPost("{id:guid}/itens")]
    public async Task<IActionResult> AdicionarItem(Guid id, [FromBody] AdicionarItemRequest req, CancellationToken ct)
    {
        await mediator.Send(new AdicionarItemVendaCommand(id, req.ProdutoId, req.Quantidade, req.PrecoUnitario, req.PercentualDesconto), ct);
        return NoContent();
    }

    /// <summary>
    /// Finaliza a venda e emite a NFC-e automaticamente.
    /// O QR Code retornado deve ser exibido na tela do PDV para o consumidor consultar.
    /// </summary>
    [HttpPost("{id:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(Guid id, [FromBody] FinalizarVendaRequest req, CancellationToken ct)
    {
        var resultado = await mediator.Send(
            new FinalizarVendaCommand(id, req.Pagamentos, req.CpfCnpjConsumidor), ct);

        // Após o MediatR publicar o VendaFinalizadaEvent, o EmitirNFCeHandler já salvou a NFC-e.
        // Buscamos os dados da nota para retornar ao PDV.
        string? qrCode = null;
        string? chaveAcesso = null;
        if (resultado.NotaFiscalId.HasValue)
        {
            var nota = await db.NotasFiscais.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == resultado.NotaFiscalId.Value, ct);
            qrCode = nota?.QrCode;
            chaveAcesso = nota?.ChaveAcesso;
        }

        return Ok(resultado with { QrCode = qrCode, ChaveAcesso = chaveAcesso });
    }

    /// <summary>Lista vendas por período.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        CancellationToken ct = default)
    {
        var resultado = await mediator.Send(new ListarVendasQuery(empresaId, inicio, fim), ct);
        return Ok(resultado);
    }
}

public record AdicionarItemRequest(Guid ProdutoId, decimal Quantidade, decimal? PrecoUnitario = null, decimal PercentualDesconto = 0);
public record FinalizarVendaRequest(IList<PagamentoDto> Pagamentos, string? CpfCnpjConsumidor = null);
