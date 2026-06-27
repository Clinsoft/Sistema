using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Vendas.Commands;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Vendas;

[ApiController]
[Route("api/devolucoes")]
[Authorize]
public class DevolucoesController(IMediator mediator, SistemaDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarDevolucaoCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Ok(new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim, CancellationToken ct)
    {
        var query = db.DevolucoesVenda.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId);

        if (inicio.HasValue) query = query.Where(d => d.DataHora >= inicio.Value);
        if (fim.HasValue) query = query.Where(d => d.DataHora <= fim.Value.AddDays(1));

        var lista = await query
            .OrderByDescending(d => d.DataHora)
            .Select(d => new
            {
                d.Id, d.NumeroVenda, d.VendaId, d.DataHora,
                d.Motivo, d.TotalDevolvido, d.ClienteId,
            })
            .ToListAsync(ct);

        return Ok(lista);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var dev = await db.DevolucoesVenda
            .Include(d => d.Itens)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        return dev is null ? NotFound() : Ok(dev);
    }

    /// <summary>Retorna os itens de uma venda para pré-selecionar na devolução.</summary>
    [HttpGet("venda/{vendaId:guid}/itens")]
    public async Task<IActionResult> ItensVenda(Guid vendaId, CancellationToken ct)
    {
        var itens = await db.ItensVenda.AsNoTracking()
            .Where(i => i.VendaId == vendaId)
            .Select(i => new
            {
                i.ProdutoId, i.Descricao, i.Quantidade,
                i.PrecoUnitario, i.Total,
            })
            .ToListAsync(ct);

        return Ok(itens);
    }
}
