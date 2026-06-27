using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Application.Estoque.Commands;
using Sistema.Domain.Estoque.Interfaces;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/produtos/{produtoId:guid}/qrcode")]
[Authorize]
public class QrCodeController(IMediator mediator, IQrCodeProdutoRepository repo) : ControllerBase
{
    /// <summary>Gera ou atualiza o QR Code do produto.</summary>
    [HttpPost]
    public async Task<IActionResult> Gerar(Guid produtoId, [FromBody] GerarQrCodeRequest req, CancellationToken ct)
    {
        var resultado = await mediator.Send(new GerarQrCodeCommand(produtoId, req.UrlBase), ct);
        return Ok(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Obter(Guid produtoId, CancellationToken ct)
    {
        var qr = await repo.ObterPorProdutoAsync(produtoId, ct);
        if (qr is null) return NotFound();
        return Ok(new { qr.Slug, qr.UrlPublica, qr.QrCodeBase64, qr.TotalScans, qr.UltimoScan });
    }
}

public record GerarQrCodeRequest(string UrlBase);
