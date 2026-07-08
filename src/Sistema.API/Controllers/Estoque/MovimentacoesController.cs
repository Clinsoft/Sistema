using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Estoque.Commands;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/movimentacoes")]
[Route("api/estoque/movimentacoes")]
[Authorize]
public class MovimentacoesController(IMediator mediator, IMovimentacaoEstoqueRepository repo, SistemaDbContext db) : ControllerBase
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

    /// <summary>Lista movimentações por período (aceita de/ate ou inicio/fim), com produto, usuário e filtros.</summary>
    [HttpGet]
    public async Task<IActionResult> ListarPorPeriodo(
        [FromQuery] Guid empresaId,
        [FromQuery] DateTime? de, [FromQuery] DateTime? ate,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim,
        [FromQuery] string? tipo, [FromQuery] string? q,
        CancellationToken ct)
    {
        var dtInicio = de ?? inicio ?? DateTime.Today.AddMonths(-1);
        var dtFim = ate ?? fim ?? DateTime.Today;
        var movs = (await repo.ListarPorPeriodoAsync(empresaId, dtInicio, dtFim.AddDays(1), ct)).ToList();

        // Filtro por tipo (colapsa AjustePositivo/AjusteNegativo em "Ajuste")
        if (!string.IsNullOrWhiteSpace(tipo))
            movs = movs.Where(m => TipoRotulo(m.Tipo) == tipo).ToList();

        var produtoIds = movs.Select(m => m.ProdutoId).Distinct().ToList();
        var usuarioIds = movs.Where(m => m.UsuarioId.HasValue).Select(m => m.UsuarioId!.Value).Distinct().ToList();
        var produtos = produtoIds.Count > 0
            ? await db.Produtos.AsNoTracking().Where(p => produtoIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Descricao, ct)
            : new();
        var usuarios = usuarioIds.Count > 0
            ? await db.Usuarios.AsNoTracking().Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Nome, ct)
            : new();
        var localIds = movs.Select(m => m.LocalEstoqueId).Distinct().ToList();
        var locais = localIds.Count > 0
            ? await db.LocaisEstoque.AsNoTracking().Where(l => localIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Nome, ct)
            : new();

        var resultado = movs.Select(m => new
        {
            m.Id, m.ProdutoId, m.LocalEstoqueId,
            produtoNome = produtos.GetValueOrDefault(m.ProdutoId, "Produto"),
            localEstoque = locais.GetValueOrDefault(m.LocalEstoqueId, "—"),
            usuarioNome = m.UsuarioId.HasValue ? usuarios.GetValueOrDefault(m.UsuarioId.Value, "—") : "—",
            tipo = TipoRotulo(m.Tipo),
            // Quantidade sinalizada: saída/ajuste negativo ficam negativos
            quantidade = m.Tipo is TipoMovimentacao.Saida or TipoMovimentacao.AjusteNegativo
                ? -m.Quantidade : m.Quantidade,
            m.CustoUnitario, m.DocumentoOrigem, m.Observacao,
            dataHora = m.CriadoEm
        });

        // Filtro por texto do produto
        if (!string.IsNullOrWhiteSpace(q))
            resultado = resultado.Where(x => x.produtoNome.Contains(q, StringComparison.OrdinalIgnoreCase));

        return Ok(resultado.OrderByDescending(x => x.dataHora).ToList());
    }

    private static string TipoRotulo(TipoMovimentacao t) => t switch
    {
        TipoMovimentacao.AjustePositivo or TipoMovimentacao.AjusteNegativo => "Ajuste",
        _ => t.ToString()
    };
}
