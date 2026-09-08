using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

/// <summary>Histórico de faturamento mensal por loja (importado + real do sistema).</summary>
[ApiController]
[Route("api/relatorios/historico-faturamento")]
[Authorize(Roles = "Administrador,Financeiro,Contador")]
public class HistoricoFaturamentoController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lojas = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId).ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        var importado = await db.HistoricoFaturamentoLoja.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId).ToListAsync(ct);

        var sistema = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada)
            .GroupBy(v => new { v.LocalEstoqueId, v.DataHora.Year, v.DataHora.Month })
            .Select(g => new { g.Key.LocalEstoqueId, g.Key.Year, g.Key.Month, Total = g.Sum(v => v.Total) })
            .ToListAsync(ct);

        // Mescla: importado prevalece; senão, o real do sistema.
        var mapa = new Dictionary<(Guid, int, int), (decimal valor, string origem)>();
        foreach (var s in sistema)
            mapa[(s.LocalEstoqueId, s.Year, s.Month)] = (Math.Round(s.Total, 2), "Sistema");
        foreach (var h in importado)
            mapa[(h.LocalEstoqueId, h.Ano, h.Mes)] = (h.Faturamento, "Importado");

        var series = mapa
            .Where(x => lojas.ContainsKey(x.Key.Item1))
            .GroupBy(x => x.Key.Item1)
            .Select(g => new
            {
                lojaId = g.Key,
                loja = lojas[g.Key],
                meses = g.OrderBy(x => x.Key.Item2).ThenBy(x => x.Key.Item3)
                    .Select(x => new
                    {
                        ano = x.Key.Item2, mes = x.Key.Item3,
                        competencia = $"{x.Key.Item2}-{x.Key.Item3:00}",
                        faturamento = x.Value.valor, origem = x.Value.origem
                    }).ToList()
            })
            .OrderByDescending(x => x.meses.Sum(m => m.faturamento))
            .ToList();

        return Ok(new { lojas = series });
    }

    /// <summary>Importa/atualiza faturamento histórico (upsert por loja/ano/mês).</summary>
    [HttpPost("importar")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Importar([FromBody] ImportarHistoricoRequest req, CancellationToken ct)
    {
        var existentes = await db.HistoricoFaturamentoLoja
            .Where(h => h.EmpresaId == req.EmpresaId && h.LocalEstoqueId == req.LocalEstoqueId)
            .ToListAsync(ct);
        var gravados = 0;
        foreach (var m in req.Meses)
        {
            var e = existentes.FirstOrDefault(x => x.Ano == m.Ano && x.Mes == m.Mes);
            if (e is null)
                db.HistoricoFaturamentoLoja.Add(HistoricoFaturamentoLoja.Criar(req.EmpresaId, req.LocalEstoqueId, m.Ano, m.Mes, m.Faturamento));
            else e.Atualizar(m.Faturamento);
            gravados++;
        }
        await db.SaveChangesAsync(ct);
        return Ok(new { gravados });
    }
}

public record ImportarHistoricoRequest(Guid EmpresaId, Guid LocalEstoqueId, List<MesFaturamento> Meses);
public record MesFaturamento(int Ano, int Mes, decimal Faturamento);
