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
        if (fim.HasValue) query = query.Where(d => d.DataHora < fim.Value.AddDays(1));

        var lista = await query
            .OrderByDescending(d => d.DataHora)
            .Select(d => new
            {
                d.Id, d.NumeroVenda, d.VendaId, d.DataHora,
                d.Motivo, d.TotalDevolvido, d.ClienteId, d.ReporEstoque,
            })
            .ToListAsync(ct);

        var clienteIds = lista.Where(d => d.ClienteId.HasValue)
            .Select(d => d.ClienteId!.Value).Distinct().ToList();
        var nomes = clienteIds.Count > 0
            ? await db.Clientes.AsNoTracking()
                .Where(c => clienteIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Nome, ct)
            : new Dictionary<Guid, string>();

        return Ok(lista.Select(d => new
        {
            d.Id, d.NumeroVenda, d.VendaId, d.DataHora,
            d.Motivo, d.TotalDevolvido, d.ClienteId, d.ReporEstoque,
            clienteNome = d.ClienteId.HasValue && nomes.TryGetValue(d.ClienteId.Value, out var n) ? n : null,
        }));
    }

    /// <summary>Exclui (estorna) uma devolução: remove o crédito e reverte a reposição de estoque, se houve.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var dev = await db.DevolucoesVenda
            .Include(d => d.Itens)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dev is null) return NotFound(new { mensagem = "Devolução não encontrada." });

        // Se a devolução repôs estoque, reverte: baixa a quantidade e registra movimentação de estorno.
        if (dev.ReporEstoque)
        {
            var localVenda = await db.Vendas.AsNoTracking()
                .Where(v => v.Id == dev.VendaId).Select(v => v.LocalEstoqueId).FirstOrDefaultAsync(ct);

            foreach (var item in dev.Itens)
            {
                var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == item.ProdutoId, ct);
                if (produto is not null)
                {
                    produto.AjustarEstoque(-item.Quantidade);
                    db.MovimentacoesEstoque.Add(Domain.Estoque.Entities.MovimentacaoEstoque.Criar(
                        dev.EmpresaId, item.ProdutoId, localVenda,
                        Domain.Estoque.Entities.TipoMovimentacao.Saida, item.Quantidade,
                        item.ValorUnitario, documentoOrigem: $"EST-DEV-{dev.NumeroVenda}",
                        observacao: "Estorno de devolução excluída"));
                }
            }
        }

        db.ItensDevolucoesVenda.RemoveRange(dev.Itens);
        db.DevolucoesVenda.Remove(dev);
        await db.SaveChangesAsync(ct);
        return NoContent();
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
