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

    /// <summary>Movimento de vendas por dia da semana × hora, separado por loja
    /// (para descobrir os horários/dias de pico).</summary>
    [HttpGet("movimento")]
    public async Task<IActionResult> Movimento([FromQuery] Guid empresaId,
        [FromQuery] int dias = 90, CancellationToken ct = default)
    {
        if (dias < 7) dias = 7;
        if (dias > 365) dias = 365;
        var inicio = DateTime.Today.AddDays(-dias);

        // Puxa só o necessário e agrega em memória (EF não traduz DateTime.DayOfWeek pro SQL).
        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == StatusVenda.Finalizada
                && v.DataHora >= inicio)
            .Select(v => new { v.LocalEstoqueId, v.DataHora, v.Total })
            .ToListAsync(ct);

        var brutos = vendas
            .GroupBy(v => new { v.LocalEstoqueId, Dia = (int)v.DataHora.DayOfWeek, Hora = v.DataHora.Hour })
            .Select(g => new
            {
                g.Key.LocalEstoqueId,
                Dia = g.Key.Dia,              // 0=Domingo … 6=Sábado
                Hour = g.Key.Hora,
                Vendas = g.Count(),
                Faturamento = g.Sum(x => x.Total)
            })
            .ToList();

        var nomes = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId)
            .ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        var diasSemana = new[] { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" };
        var diasCurto = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

        var lojas = brutos
            .GroupBy(x => x.LocalEstoqueId)
            .Select(g =>
            {
                var totalVendas = g.Sum(x => x.Vendas);
                var faturamento = g.Sum(x => x.Faturamento);

                var porDia = Enumerable.Range(0, 7).Select(d =>
                {
                    var itens = g.Where(x => x.Dia == d).ToList();
                    return new { dia = d, label = diasCurto[d], vendas = itens.Sum(x => x.Vendas), faturamento = itens.Sum(x => x.Faturamento) };
                }).ToList();

                var porHora = g.GroupBy(x => x.Hour).OrderBy(h => h.Key)
                    .Select(h => new { hora = h.Key, vendas = h.Sum(x => x.Vendas), faturamento = h.Sum(x => x.Faturamento) })
                    .ToList();

                var picoDiaRaw = porDia.OrderByDescending(d => d.vendas).First();
                var picoHoraRaw = porHora.OrderByDescending(h => h.vendas).FirstOrDefault();

                return new
                {
                    localEstoqueId = g.Key,
                    nome = nomes.TryGetValue(g.Key, out var n) ? n : "Loja",
                    totalVendas,
                    faturamento,
                    picoDia = new { picoDiaRaw.dia, label = diasSemana[picoDiaRaw.dia], picoDiaRaw.vendas, picoDiaRaw.faturamento },
                    picoHora = picoHoraRaw == null ? null
                        : new { picoHoraRaw.hora, label = $"{picoHoraRaw.hora}h", picoHoraRaw.vendas, picoHoraRaw.faturamento },
                    porDia,
                    porHora,
                    heatmap = g.Select(x => new { dia = x.Dia, hora = x.Hour, vendas = x.Vendas, faturamento = x.Faturamento }).ToList()
                };
            })
            .OrderByDescending(l => l.totalVendas)
            .ToList();

        return Ok(new { periodoDias = dias, lojas });
    }
}
