using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/financeiro/recebiveis-cartao")]
[Authorize]
public class ReceiveisCartaoController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] Guid? operadoraId,
        [FromQuery] string? status,
        [FromQuery] string? formaPagamento,
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        CancellationToken ct)
    {
        var query = db.ReceiveisCartao.AsNoTracking()
            .Include(r => r.Operadora)
            .Where(r => r.EmpresaId == empresaId);

        if (operadoraId.HasValue)
            query = query.Where(r => r.OperadoraCartaoId == operadoraId.Value);

        // "A Receber" (UI) equivale a Pendente (domínio)
        var statusDominio = status == "A Receber" ? "Pendente" : status;
        if (Enum.TryParse<StatusRecebivelCartao>(statusDominio, out var st))
            query = query.Where(r => r.Status == st);

        if (!string.IsNullOrWhiteSpace(formaPagamento))
            query = query.Where(r => r.FormaPagamento == formaPagamento);

        if (inicio.HasValue) query = query.Where(r => r.DataTransacao >= inicio.Value);
        if (fim.HasValue)    query = query.Where(r => r.DataTransacao <= fim.Value.AddDays(1));

        var raw = await query
            .OrderByDescending(r => r.DataPrevistaRepasse)
            .Take(500)
            .Select(r => new
            {
                r.Id, r.VendaId, r.FormaPagamento, r.Parcelas,
                r.ValorBruto, r.Taxa, r.ValorLiquido,
                r.DataTransacao, r.DataPrevistaRepasse, r.DataRepasse,
                r.TaxaAntecipacaoAplicada, r.Status,
                operadora = r.Operadora != null ? r.Operadora.Nome : "",
            })
            .ToListAsync(ct);

        // Resolve o número da venda
        var vendaIds = raw.Where(r => r.VendaId.HasValue).Select(r => r.VendaId!.Value).Distinct().ToList();
        var numeros = vendaIds.Count > 0
            ? await db.Vendas.AsNoTracking().Where(v => vendaIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.Numero, ct)
            : new Dictionary<Guid, string>();

        var recebiveis = raw.Select(r =>
        {
            var valorTaxa = Math.Round(r.ValorBruto - r.ValorLiquido, 2);
            decimal descAntecip = 0;
            if (r.TaxaAntecipacaoAplicada is { } tx && tx > 0 && tx < 100)
            {
                var liquidoAntes = r.ValorLiquido / (1 - tx / 100m);
                descAntecip = Math.Round(liquidoAntes - r.ValorLiquido, 2);
            }
            return new
            {
                r.Id, r.VendaId,
                numeroVenda = r.VendaId.HasValue && numeros.TryGetValue(r.VendaId.Value, out var n) ? n : "—",
                operadora = r.operadora,
                bandeira = (string?)null,
                formaPagamento = r.FormaPagamento,
                parcelas = r.Parcelas,
                dataVenda = r.DataTransacao.ToString("yyyy-MM-dd"),
                valorBruto = r.ValorBruto,
                taxaTotal = r.Taxa,
                valorTaxa,
                taxaOperadora = r.Taxa,
                valorTaxaOperadora = valorTaxa,
                valorLiquido = r.ValorLiquido,
                dataPrevistaCredito = r.DataPrevistaRepasse.ToString("yyyy-MM-dd"),
                dataEfetiva = r.DataRepasse?.ToString("yyyy-MM-dd"),
                status = r.Status == StatusRecebivelCartao.Pendente ? "A Receber" : r.Status.ToString(),
                taxaAntecipacao = r.TaxaAntecipacaoAplicada,
                valorDescontoAntecipacao = descAntecip,
            };
        }).ToList();

        return Ok(recebiveis);
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarRecebivelRequest req, CancellationToken ct)
    {
        var recebivel = RecebivelCartao.Criar(
            req.EmpresaId, req.OperadoraCartaoId, req.VendaId,
            req.FormaPagamento, req.Parcelas, req.ValorBruto, req.Taxa,
            req.DataTransacao, req.DataPrevistaRepasse, req.NsuTid);
        db.ReceiveisCartao.Add(recebivel);
        await db.SaveChangesAsync(ct);
        return Ok(new { recebivel.Id, recebivel.ValorLiquido });
    }

    [HttpPost("receber")]
    public async Task<IActionResult> MarcarRecebido([FromBody] IdsRequest req, CancellationToken ct)
    {
        var itens = await db.ReceiveisCartao
            .Where(r => req.Ids.Contains(r.Id) && r.Status == StatusRecebivelCartao.Pendente)
            .ToListAsync(ct);
        foreach (var r in itens) r.MarcarRecebido();
        await db.SaveChangesAsync(ct);
        return Ok(new { atualizados = itens.Count });
    }

    [HttpPost("antecipar")]
    public async Task<IActionResult> Antecipar([FromBody] AnteciparRequest req, CancellationToken ct)
    {
        var itens = await db.ReceiveisCartao
            .Where(r => req.Ids.Contains(r.Id) && r.Status == StatusRecebivelCartao.Pendente)
            .ToListAsync(ct);
        foreach (var r in itens) r.MarcarAntecipado(req.TaxaAntecipacao);
        await db.SaveChangesAsync(ct);
        return Ok(new { atualizados = itens.Count });
    }

    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] IdsRequest req, CancellationToken ct)
    {
        var itens = await db.ReceiveisCartao
            .Where(r => req.Ids.Contains(r.Id) && r.Status != StatusRecebivelCartao.Cancelado)
            .ToListAsync(ct);
        foreach (var r in itens) r.Cancelar();
        await db.SaveChangesAsync(ct);
        return Ok(new { atualizados = itens.Count });
    }
}

public record RegistrarRecebivelRequest(
    Guid EmpresaId, Guid OperadoraCartaoId, Guid? VendaId,
    string FormaPagamento, int Parcelas, decimal ValorBruto, decimal Taxa,
    DateTime DataTransacao, DateTime DataPrevistaRepasse, string? NsuTid = null);

public record IdsRequest(List<Guid> Ids);
public record AnteciparRequest(List<Guid> Ids, decimal TaxaAntecipacao);
