using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

/// <summary>Planejamento anual de vendas — metas mensais salvas e editáveis pelo lojista.</summary>
[ApiController]
[Route("api/relatorios/planejamento-anual")]
[Authorize(Roles = "Administrador,Financeiro")]
public class PlanejamentoAnualController(SistemaDbContext db) : ControllerBase
{
    private static readonly string[] NomesMes =
        { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };

    /// <summary>Retorna as metas salvas do ano + o realizado de cada mês (das vendas).</summary>
    [HttpGet]
    public async Task<IActionResult> Obter([FromQuery] Guid empresaId, [FromQuery] int ano, CancellationToken ct)
    {
        var metasSalvas = await db.MetasVendaMensal.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Ano == ano)
            .ToDictionaryAsync(m => m.Mes, m => m.Valor, ct);

        var inicio = new DateTime(ano, 1, 1);
        var fim = new DateTime(ano, 12, 31, 23, 59, 59);
        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && v.DataHora >= inicio && v.DataHora <= fim)
            .Select(v => new { v.DataHora.Month, v.Total })
            .ToListAsync(ct);

        var meses = Enumerable.Range(1, 12).Select(mes => new
        {
            mes,
            nomeMes = NomesMes[mes - 1],
            meta = metasSalvas.GetValueOrDefault(mes, 0m),
            realizado = vendas.Where(v => v.Month == mes).Sum(v => v.Total)
        }).ToList();

        if (metasSalvas.Count == 0) return Ok(null);   // sem plano salvo → o Dashboard mostra "Criar Planejamento"

        return Ok(new
        {
            ano,
            meses,
            totalMeta = meses.Sum(m => m.meta),
            totalRealizado = meses.Sum(m => m.realizado)
        });
    }

    /// <summary>Salva/atualiza as metas mensais do ano (upsert).</summary>
    [HttpPost]
    public async Task<IActionResult> Salvar([FromBody] SalvarPlanejamentoRequest req, CancellationToken ct)
    {
        var existentes = await db.MetasVendaMensal
            .Where(m => m.EmpresaId == req.EmpresaId && m.Ano == req.Ano)
            .ToListAsync(ct);

        foreach (var item in req.Metas)
        {
            var atual = existentes.FirstOrDefault(m => m.Mes == item.Mes);
            if (atual is null)
                db.MetasVendaMensal.Add(MetaVendaMensal.Criar(req.EmpresaId, req.Ano, item.Mes, item.Valor));
            else
                atual.DefinirValor(item.Valor);
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { salvo = true, quantidade = req.Metas.Count });
    }

    /// <summary>Exclui o plano do ano (todas as metas mensais).</summary>
    [HttpDelete]
    public async Task<IActionResult> Excluir([FromQuery] Guid empresaId, [FromQuery] int ano, CancellationToken ct)
    {
        var metas = await db.MetasVendaMensal
            .Where(m => m.EmpresaId == empresaId && m.Ano == ano)
            .ToListAsync(ct);
        db.MetasVendaMensal.RemoveRange(metas);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record SalvarPlanejamentoRequest(Guid EmpresaId, int Ano, List<MetaMensalItem> Metas);
public record MetaMensalItem(int Mes, decimal Valor);
