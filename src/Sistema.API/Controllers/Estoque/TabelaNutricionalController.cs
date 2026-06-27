using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Application.Estoque.Commands;
using Sistema.Domain.Estoque.Interfaces;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/produtos/{produtoId:guid}")]
[Authorize]
public class TabelaNutricionalController(IMediator mediator, ITabelaNutricionalRepository tabelaRepo,
    IReceitaProdutoRepository receitaRepo, ISugestaoProdutoRepository sugestaoRepo) : ControllerBase
{
    // ─── Tabela Nutricional ───────────────────────────────────────────────────

    [HttpGet("nutricional")]
    public async Task<IActionResult> ObterNutricional(Guid produtoId, CancellationToken ct)
    {
        var t = await tabelaRepo.ObterPorProdutoAsync(produtoId, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPut("nutricional")]
    public async Task<IActionResult> SalvarNutricional(Guid produtoId,
        [FromBody] SalvarTabelaNutricionalCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd with { ProdutoId = produtoId }, ct);
        return Ok(new { id });
    }

    // ─── Receitas ─────────────────────────────────────────────────────────────

    [HttpGet("receitas")]
    public async Task<IActionResult> ListarReceitas(Guid produtoId, CancellationToken ct)
        => Ok(await receitaRepo.ListarPorProdutoAsync(produtoId, ct));

    [HttpPost("receitas")]
    public async Task<IActionResult> AdicionarReceita(Guid produtoId,
        [FromBody] SalvarReceitaCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd with { ProdutoId = produtoId }, ct);
        return Ok(new { id });
    }

    // ─── Sugestões ────────────────────────────────────────────────────────────

    [HttpGet("sugestoes")]
    public async Task<IActionResult> ListarSugestoes(Guid produtoId, CancellationToken ct)
        => Ok(await sugestaoRepo.ListarPorProdutoAsync(produtoId, ct));

    [HttpPost("sugestoes")]
    public async Task<IActionResult> AdicionarSugestao(Guid produtoId,
        [FromBody] AdicionarSugestaoCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd with { ProdutoId = produtoId }, ct);
        return Ok(new { id });
    }
}
