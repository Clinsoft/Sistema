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
    public async Task<IActionResult> Obter([FromQuery] Guid empresaId, [FromQuery] int ano,
        [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        // localEstoqueId informado = metas daquela loja; ausente = soma das lojas (rede)
        var metasSalvas = await db.MetasVendaMensal.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Ano == ano
                && (localEstoqueId == null || m.LocalEstoqueId == localEstoqueId))
            .GroupBy(m => m.Mes)
            .Select(g => new { Mes = g.Key, Valor = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.Mes, x => x.Valor, ct);

        var inicio = new DateTime(ano, 1, 1);
        var fim = new DateTime(ano, 12, 31, 23, 59, 59);
        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && (localEstoqueId == null || v.LocalEstoqueId == localEstoqueId)
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

    /// <summary>Calendário de compras: para o mês pedido, mostra em que DIA os boletos a pagar se
    /// concentram (dias a evitar) e os dias leves, além dos melhores dias para comprar — considerando
    /// o prazo médio dos fornecedores — para o boleto cair num dia leve. Recalcula a cada mês.</summary>
    [HttpGet("calendario-compras")]
    public async Task<IActionResult> CalendarioCompras([FromQuery] Guid empresaId, [FromQuery] int ano,
        [FromQuery] int mes, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        if (mes < 1 || mes > 12) return BadRequest("Mês inválido.");
        var ini = new DateTime(ano, mes, 1);
        var fim = ini.AddMonths(1);
        var diasNoMes = DateTime.DaysInMonth(ano, mes);

        // Prazo médio dos fornecedores (emissão → vencimento), últimos 365 dias.
        var desde = DateTime.Today.AddDays(-365);
        var prazos = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == Domain.Financeiro.Entities.TipoLancamento.ContaPagar
                && l.Status != Domain.Financeiro.Entities.StatusLancamento.Cancelado
                && l.FornecedorId != null && l.DataEmissao >= desde)
            .Select(l => EF.Functions.DateDiffDay(l.DataEmissao, l.DataVencimento))
            .ToListAsync(ct);
        var prazoValidos = prazos.Where(p => p > 0 && p <= 120).ToList();
        var prazoMedio = prazoValidos.Count > 0 ? (int)Math.Round(prazoValidos.Average()) : 28;

        // MODELO DE PROJEÇÃO: cada dia de COMPRA deste mês projeta o vencimento (compra + prazo médio).
        // A janela de vencimentos possíveis vai de (1º do mês + prazo) até (fim do mês + prazo).
        var dueIni = ini.AddDays(prazoMedio).Date;
        var dueFim = fim.AddDays(prazoMedio).Date;
        var vencimentos = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == Domain.Financeiro.Entities.TipoLancamento.ContaPagar
                && (l.Status == Domain.Financeiro.Entities.StatusLancamento.EmAberto
                    || l.Status == Domain.Financeiro.Entities.StatusLancamento.PagoParcialmente)
                && (localEstoqueId == null || l.LocalEstoqueId == localEstoqueId)
                && l.DataVencimento >= dueIni && l.DataVencimento <= dueFim)
            .Select(l => new { Data = l.DataVencimento.Date, Saldo = l.ValorOriginal - l.ValorPago })
            .ToListAsync(ct);
        var totalPorData = vencimentos.GroupBy(v => v.Data)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Saldo));

        var hoje = DateTime.Today;
        var projecao = Enumerable.Range(1, diasNoMes).Select(d =>
        {
            var compra = new DateTime(ano, mes, d);
            var venc = compra.AddDays(prazoMedio).Date;
            return new
            {
                dia = d,
                venc,
                total = totalPorData.GetValueOrDefault(venc, 0m),
                passado = compra.Date < hoje,
            };
        }).ToList();

        // Limiares calculados só sobre dias de compra válidos (futuros) com vencimento carregado.
        var valores = projecao.Where(p => !p.passado && p.total > 0).Select(p => p.total).ToList();
        var maxV = valores.Count > 0 ? valores.Max() : 0m;
        var mediaV = valores.Count > 0 ? valores.Average() : 0m;

        string Nivel(decimal total, bool passado)
        {
            if (passado) return "passado";
            if (total <= 0) return "leve";
            if (total >= 15000m || (maxV > 0 && total >= 0.5m * maxV)) return "pesado";
            if (total > mediaV) return "medio";
            return "leve";
        }

        var dias = projecao.Select(p => new
        {
            dia = p.dia,
            nivel = Nivel(p.total, p.passado),
            vencDia = p.venc.Day,
            vencMes = p.venc.Month,
            total = Math.Round(p.total, 2),   // quanto já vence no dia projetado (saída concorrente)
        }).ToList();

        var melhoresDiasCompra = dias.Where(d => d.nivel == "leve").Select(d => d.dia).ToList();
        var diasEvitarCompra = dias.Where(d => d.nivel == "pesado").Select(d => d.dia).ToList();

        return Ok(new
        {
            ano, mes, nomeMes = NomesMes[mes - 1], diasNoMes, prazoMedio,
            dias, melhoresDiasCompra, diasEvitarCompra,
            totalMes = Math.Round(totalPorData.Values.Sum(), 2)
        });
    }

    /// <summary>Salva/atualiza as metas mensais do ano (upsert).</summary>
    [HttpPost]
    public async Task<IActionResult> Salvar([FromBody] SalvarPlanejamentoRequest req, CancellationToken ct)
    {
        var existentes = await db.MetasVendaMensal
            .Where(m => m.EmpresaId == req.EmpresaId && m.Ano == req.Ano && m.LocalEstoqueId == req.LocalEstoqueId)
            .ToListAsync(ct);

        foreach (var item in req.Metas)
        {
            var atual = existentes.FirstOrDefault(m => m.Mes == item.Mes);
            if (atual is null)
                db.MetasVendaMensal.Add(MetaVendaMensal.Criar(req.EmpresaId, req.Ano, item.Mes, item.Valor, req.LocalEstoqueId));
            else
                atual.DefinirValor(item.Valor);
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { salvo = true, quantidade = req.Metas.Count });
    }

    /// <summary>Exclui o plano do ano (todas as metas mensais).</summary>
    [HttpDelete]
    public async Task<IActionResult> Excluir([FromQuery] Guid empresaId, [FromQuery] int ano,
        [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var metas = await db.MetasVendaMensal
            .Where(m => m.EmpresaId == empresaId && m.Ano == ano
                && (localEstoqueId == null || m.LocalEstoqueId == localEstoqueId))
            .ToListAsync(ct);
        db.MetasVendaMensal.RemoveRange(metas);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record SalvarPlanejamentoRequest(Guid EmpresaId, int Ano, List<MetaMensalItem> Metas, Guid? LocalEstoqueId = null);
public record MetaMensalItem(int Mes, decimal Valor);
