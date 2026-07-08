using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>Fluxo de Caixa — realizado e projetado.</summary>
[ApiController]
[Route("api/financeiro/fluxo-caixa")]
[Authorize]
public class FluxoCaixaController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Fluxo realizado — movimentações efetivas (vendas e pagamentos) no período.</summary>
    [HttpGet("realizado")]
    public async Task<IActionResult> Realizado([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var fimExcl = fim.AddDays(1);
        var movs = new List<(DateTime data, string descricao, string? categoria, decimal entradas, decimal saidas)>();

        // Entradas: vendas finalizadas
        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && v.DataHora >= inicio && v.DataHora < fimExcl)
            .Select(v => new { v.DataHora, v.Numero, v.Total })
            .ToListAsync(ct);
        foreach (var v in vendas)
            movs.Add((v.DataHora.Date, $"Venda {v.Numero}", "Recebimentos", v.Total, 0));

        // Entradas: contas a receber baixadas (crediário/faturado)
        var recebidos = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaReceber
                && l.DataPagamento != null && l.DataPagamento >= inicio && l.DataPagamento < fimExcl
                && l.ValorPago > 0)
            .Select(l => new { l.DataPagamento, l.Descricao, l.Categoria, l.ValorPago })
            .ToListAsync(ct);
        foreach (var l in recebidos)
            movs.Add((l.DataPagamento!.Value.Date, l.Descricao, l.Categoria ?? "Recebimentos", l.ValorPago, 0));

        // Saídas: contas a pagar baixadas
        var pagos = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaPagar
                && l.DataPagamento != null && l.DataPagamento >= inicio && l.DataPagamento < fimExcl
                && l.ValorPago > 0)
            .Select(l => new { l.DataPagamento, l.Descricao, l.Categoria, l.ValorPago })
            .ToListAsync(ct);
        foreach (var l in pagos)
            movs.Add((l.DataPagamento!.Value.Date, l.Descricao, l.Categoria ?? "Despesas Variáveis", 0, l.ValorPago));

        var saldoInicial = await db.ContasBancarias.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .SumAsync(c => (decimal?)c.SaldoAtual ?? 0, ct);

        return Ok(MontarLinhas(movs, saldoInicial));
    }

    /// <summary>Fluxo projetado — contas em aberto por vencimento no período.</summary>
    [HttpGet("projetado")]
    public async Task<IActionResult> Projetado([FromQuery] Guid empresaId,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim, [FromQuery] DateTime? ate, CancellationToken ct)
    {
        var de = inicio ?? DateTime.Today;
        var limite = fim ?? ate ?? DateTime.Today.AddDays(30);
        var fimExcl = limite.AddDays(1);
        var movs = new List<(DateTime data, string descricao, string? categoria, decimal entradas, decimal saidas)>();

        var receber = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaReceber
                && l.Status == StatusLancamento.EmAberto
                && l.DataVencimento >= de && l.DataVencimento < fimExcl)
            .Select(l => new { l.DataVencimento, l.Descricao, l.Categoria, saldo = l.ValorOriginal - l.ValorPago })
            .ToListAsync(ct);
        foreach (var l in receber)
            movs.Add((l.DataVencimento.Date, l.Descricao, l.Categoria ?? "Recebimentos", l.saldo, 0));

        var pagar = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaPagar
                && l.Status == StatusLancamento.EmAberto
                && l.DataVencimento >= de && l.DataVencimento < fimExcl)
            .Select(l => new { l.DataVencimento, l.Descricao, l.Categoria, saldo = l.ValorOriginal - l.ValorPago })
            .ToListAsync(ct);
        foreach (var l in pagar)
            movs.Add((l.DataVencimento.Date, l.Descricao, l.Categoria ?? "Despesas Variáveis", 0, l.saldo));

        var saldoInicial = await db.ContasBancarias.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .SumAsync(c => (decimal?)c.SaldoAtual ?? 0, ct);

        return Ok(MontarLinhas(movs, saldoInicial));
    }

    /// <summary>Ordena as movimentações por data e calcula saldo e acumulado.</summary>
    private static object MontarLinhas(
        List<(DateTime data, string descricao, string? categoria, decimal entradas, decimal saidas)> movs,
        decimal saldoInicial)
    {
        decimal acumulado = saldoInicial;
        var linhas = movs.OrderBy(m => m.data).Select(m =>
        {
            var saldo = m.entradas - m.saidas;
            acumulado += saldo;
            return new
            {
                data = m.data.ToString("yyyy-MM-dd"),
                m.descricao, m.categoria,
                m.entradas, m.saidas, saldo, acumulado
            };
        }).ToList();

        return new
        {
            saldoInicial,
            totalEntradas = movs.Sum(m => m.entradas),
            totalSaidas = movs.Sum(m => m.saidas),
            saldoFinal = acumulado,
            linhas
        };
    }
}
