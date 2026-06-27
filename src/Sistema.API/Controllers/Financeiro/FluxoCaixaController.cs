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
    /// <summary>Fluxo realizado — movimentações efetivas por dia.</summary>
    [HttpGet("realizado")]
    public async Task<IActionResult> Realizado([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        // Entradas: vendas finalizadas
        var entradas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora <= fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .GroupBy(v => v.DataHora.Date)
            .Select(g => new { data = g.Key, valor = g.Sum(v => v.Total), tipo = "Entrada" })
            .ToListAsync(ct);

        // Saídas: pagamentos de contas no período
        var saidas = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.DataPagamento >= inicio && l.DataPagamento <= fim.AddDays(1)
                && l.Status == StatusLancamento.Pago)
            .GroupBy(l => l.DataPagamento!.Value.Date)
            .Select(g => new { data = g.Key, valor = g.Sum(l => l.ValorPago), tipo = "Saída" })
            .ToListAsync(ct);

        // Saldo inicial das contas bancárias
        var saldoInicial = await db.ContasBancarias.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .SumAsync(c => (decimal?)c.SaldoAtual ?? 0, ct);

        // Montar fluxo diário acumulado
        var diasRange = Enumerable.Range(0, (fim - inicio).Days + 1)
            .Select(d => inicio.AddDays(d).Date);

        decimal saldoAcumulado = saldoInicial;
        var fluxo = diasRange.Select(dia =>
        {
            var totalEntrada = entradas.Where(e => e.data == dia).Sum(e => e.valor);
            var totalSaida = saidas.Where(s => s.data == dia).Sum(s => s.valor);
            saldoAcumulado += totalEntrada - totalSaida;
            return new
            {
                data = dia,
                entradas = totalEntrada,
                saidas = totalSaida,
                saldo = totalEntrada - totalSaida,
                saldoAcumulado
            };
        }).ToList();

        return Ok(new
        {
            periodo = new { inicio, fim },
            saldoInicial,
            totalEntradas = entradas.Sum(e => e.valor),
            totalSaidas = saidas.Sum(s => s.valor),
            saldoFinal = saldoAcumulado,
            fluxo
        });
    }

    /// <summary>Fluxo projetado — contas em aberto por vencimento.</summary>
    [HttpGet("projetado")]
    public async Task<IActionResult> Projetado([FromQuery] Guid empresaId,
        [FromQuery] DateTime? ate, CancellationToken ct)
    {
        var limiteData = ate ?? DateTime.Today.AddDays(30);

        var receber = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaReceber
                && l.Status == StatusLancamento.EmAberto
                && l.DataVencimento <= limiteData)
            .Select(l => new
            {
                l.DataVencimento, l.Descricao,
                valor = l.ValorOriginal - l.ValorPago, tipo = "Receber"
            })
            .ToListAsync(ct);

        var pagar = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.Status == StatusLancamento.EmAberto
                && l.DataVencimento <= limiteData)
            .Select(l => new
            {
                l.DataVencimento, l.Descricao,
                valor = l.ValorOriginal - l.ValorPago, tipo = "Pagar"
            })
            .ToListAsync(ct);

        return Ok(new
        {
            ate = limiteData,
            totalReceber = receber.Sum(r => r.valor),
            totalPagar = pagar.Sum(p => p.valor),
            saldoProjectado = receber.Sum(r => r.valor) - pagar.Sum(p => p.valor),
            receber, pagar
        });
    }
}
