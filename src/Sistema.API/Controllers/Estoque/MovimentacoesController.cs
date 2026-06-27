using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Application.Estoque.Commands;
using Sistema.Domain.Estoque.Interfaces;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/movimentacoes")]
[Authorize]
public class MovimentacoesController(IMediator mediator, IMovimentacaoEstoqueRepository repo) : ControllerBase
{
    /// <summary>Registra uma movimentação de estoque (entrada, saída, ajuste).</summary>
    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarMovimentacaoCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Ok(new { id });
    }

    /// <summary>Lista movimentações de um produto.</summary>
    [HttpGet("produto/{produtoId:guid}")]
    public async Task<IActionResult> ListarPorProduto(Guid produtoId, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var movs = await repo.ListarPorProdutoAsync(empresaId, produtoId, ct);
        return Ok(movs.Select(m => new
        {
            m.Id, m.ProdutoId, m.LocalEstoqueId, m.LoteId,
            m.Tipo, m.Quantidade, m.CustoUnitario,
            m.DocumentoOrigem, m.Observacao, m.CriadoEm
        }));
    }

    /// <summary>Lista movimentações por período.</summary>
    [HttpGet]
    public async Task<IActionResult> ListarPorPeriodo(
        [FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        CancellationToken ct)
    {
        var movs = await repo.ListarPorPeriodoAsync(empresaId, inicio, fim, ct);
        return Ok(movs.Select(m => new
        {
            m.Id, m.ProdutoId, m.LocalEstoqueId, m.Tipo,
            m.Quantidade, m.CustoUnitario, m.DocumentoOrigem, m.CriadoEm
        }));
    }
}
