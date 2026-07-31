using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Indicadores dos 4 cards do topo do dashboard.</summary>
    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] Guid empresaId,
        [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var hoje = DateTime.Today;
        var amanha = hoje.AddDays(1);

        var vendasHoje = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == StatusVenda.Finalizada
                && (localEstoqueId == null || v.LocalEstoqueId == localEstoqueId)
                && v.DataHora >= hoje && v.DataHora < amanha)
            .SumAsync(v => (decimal?)v.Total, ct) ?? 0m;

        var pedidosAbertos = await db.PedidosCompra.AsNoTracking()
            .CountAsync(p => p.EmpresaId == empresaId
                && p.Status == StatusPedidoCompra.Enviado, ct);

        var aReceberVencido = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaReceber
                && l.Status == StatusLancamento.EmAberto
                && l.DataVencimento < amanha)
            .SumAsync(l => (decimal?)(l.ValorOriginal - l.ValorPago), ct) ?? 0m;

        var produtosSemEstoque = await db.Produtos.AsNoTracking()
            .CountAsync(p => p.EmpresaId == empresaId && p.Ativo && p.EstoqueAtual <= 0, ct);

        return Ok(new
        {
            vendasHoje,
            pedidosAbertos,
            aReceberVencido,
            produtosSemEstoque,
        });
    }
}
