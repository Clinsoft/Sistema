using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/lotes")]
[Authorize]
public class LotesController(ILoteRepository repo, IUnitOfWork uow) : ControllerBase
{
    [HttpGet("produto/{produtoId:guid}")]
    public async Task<IActionResult> ListarPorProduto(Guid produtoId, [FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await repo.ListarPorProdutoAsync(empresaId, produtoId, ct));

    [HttpGet("vencimentos")]
    public async Task<IActionResult> Vencimentos([FromQuery] Guid empresaId, [FromQuery] int diasAlerta = 30, CancellationToken ct = default)
    {
        var lotes = await repo.ListarVencidosOuProximosAsync(empresaId, diasAlerta, ct);
        return Ok(lotes.Select(l => new
        {
            l.Id, l.ProdutoId, l.NumeroLote, l.Quantidade,
            l.DataValidade, l.DataFabricacao, l.LocalEstoqueId,
            Vencido = l.EstaVencido(),
            VenceEm30 = l.VenceEm(30)
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarLoteRequest req, CancellationToken ct)
    {
        var lote = Lote.Criar(req.EmpresaId, req.ProdutoId, req.LocalEstoqueId,
            req.NumeroLote, req.Quantidade, req.CustoUnitario,
            req.DataFabricacao, req.DataValidade);
        await repo.AdicionarAsync(lote, ct);
        await uow.SalvarAsync(ct);
        return Ok(new { lote.Id });
    }
}

public record CriarLoteRequest(
    Guid EmpresaId, Guid ProdutoId, Guid LocalEstoqueId,
    string NumeroLote, decimal Quantidade, decimal CustoUnitario,
    DateTime? DataFabricacao = null, DateTime? DataValidade = null);
