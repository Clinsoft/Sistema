using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

[ApiController]
[Route("api/relatorios/financeiro")]
[Authorize(Roles = "Administrador,Financeiro,Contador")]
public class RelatoriosFinanceiroController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Contas a receber no período com detalhe de cliente.</summary>
    [HttpGet("contas-receber")]
    public async Task<IActionResult> ContasReceber([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaReceber
                && l.DataVencimento >= inicio && l.DataVencimento < fim.AddDays(1));

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusLancamento>(status, out var st))
            query = query.Where(l => l.Status == st);

        var lancamentos = await query
            .GroupJoin(db.Clientes, l => l.ClienteId, c => c.Id, (l, cs) => new { l, cs })
            .SelectMany(x => x.cs.DefaultIfEmpty(), (x, c) => new
            {
                x.l.Id, x.l.Descricao, x.l.ValorOriginal, x.l.ValorPago,
                saldo = x.l.ValorOriginal - x.l.ValorPago,
                x.l.DataVencimento, x.l.DataPagamento, x.l.Status,
                x.l.Parcela, x.l.TotalParcelas, x.l.DocumentoOrigem,
                clienteNome = c != null ? c.Nome : "—",
                diasAtraso = x.l.DataVencimento < DateTime.Today && x.l.Status == StatusLancamento.EmAberto
                    ? (int)(DateTime.Today - x.l.DataVencimento).TotalDays : 0
            })
            .OrderBy(l => l.DataVencimento)
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            totalEmAberto = lancamentos.Where(l => l.Status == StatusLancamento.EmAberto).Sum(l => l.saldo),
            totalRecebido = lancamentos.Where(l => l.Status == StatusLancamento.Pago).Sum(l => l.ValorPago),
            qtdVencidas = lancamentos.Count(l => l.diasAtraso > 0),
            lancamentos
        });
    }

    /// <summary>Contas a pagar no período com detalhe de fornecedor.</summary>
    [HttpGet("contas-pagar")]
    public async Task<IActionResult> ContasPagar([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.DataVencimento >= inicio && l.DataVencimento < fim.AddDays(1));

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusLancamento>(status, out var st))
            query = query.Where(l => l.Status == st);

        var lancamentos = await query
            .GroupJoin(db.Fornecedores, l => l.FornecedorId, f => f.Id, (l, fs) => new { l, fs })
            .SelectMany(x => x.fs.DefaultIfEmpty(), (x, f) => new
            {
                x.l.Id, x.l.Descricao, x.l.ValorOriginal, x.l.ValorPago,
                saldo = x.l.ValorOriginal - x.l.ValorPago,
                x.l.DataVencimento, x.l.DataPagamento, x.l.Status,
                x.l.Parcela, x.l.TotalParcelas, x.l.DocumentoOrigem,
                fornecedorNome = f != null ? f.RazaoSocial : "—",
                diasAtraso = x.l.DataVencimento < DateTime.Today && x.l.Status == StatusLancamento.EmAberto
                    ? (int)(DateTime.Today - x.l.DataVencimento).TotalDays : 0
            })
            .OrderBy(l => l.DataVencimento)
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            totalEmAberto = lancamentos.Where(l => l.Status == StatusLancamento.EmAberto).Sum(l => l.saldo),
            totalPago = lancamentos.Where(l => l.Status == StatusLancamento.Pago).Sum(l => l.ValorPago),
            qtdVencidas = lancamentos.Count(l => l.diasAtraso > 0),
            lancamentos
        });
    }

    /// <summary>Despesas agrupadas por categoria no período.</summary>
    [HttpGet("despesas-por-categoria")]
    public async Task<IActionResult> DespesasPorCategoria([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var result = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.DataPagamento >= inicio && l.DataPagamento < fim.AddDays(1)
                && l.Status == StatusLancamento.Pago)
            .GroupJoin(db.CategoriasFinanceiras, l => l.CategoriaId, c => c.Id, (l, cs) => new { l, cs })
            .SelectMany(x => x.cs.DefaultIfEmpty(), (x, c) => new
            {
                categoria = c != null ? c.Nome : "Sem Categoria",
                x.l.ValorPago
            })
            .GroupBy(x => x.categoria)
            .Select(g => new { categoria = g.Key, total = g.Sum(x => x.ValorPago) })
            .OrderByDescending(x => x.total)
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            totalDespesas = result.Sum(r => r.total),
            porCategoria = result
        });
    }

    /// <summary>Clientes com informações financeiras — saldo devedor em crediário + contas a receber.</summary>
    [HttpGet("clientes-financeiro")]
    public async Task<IActionResult> ClientesFinanceiro([FromQuery] Guid empresaId, CancellationToken ct)
    {
        // SaldoDevedor = soma das parcelas em aberto por cliente
        var crediarios = await db.ParcelasCrediario.AsNoTracking()
            .Where(p => p.Status == Domain.Crediario.Entities.StatusParcela.EmAberto)
            .Join(db.Crediarios.Where(c => c.EmpresaId == empresaId),
                p => p.CrediarioId, c => c.Id,
                (p, c) => new { c.ClienteId, p.Valor })
            .GroupBy(x => x.ClienteId)
            .Select(g => new { clienteId = g.Key, saldoCrediario = g.Sum(x => x.Valor) })
            .ToListAsync(ct);

        var contasReceber = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaReceber
                && l.Status == StatusLancamento.EmAberto
                && l.ClienteId.HasValue)
            .GroupBy(l => l.ClienteId)
            .Select(g => new { clienteId = g.Key, saldoReceber = g.Sum(l => l.ValorOriginal - l.ValorPago) })
            .ToListAsync(ct);

        var clienteIds = crediarios.Select(c => c.clienteId)
            .Union(contasReceber.Select(c => c.clienteId ?? Guid.Empty))
            .Distinct().ToList();

        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => clienteIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Nome, c.CpfCnpj, c.Telefone, c.Celular })
            .ToListAsync(ct);

        var resultado = clientes.Select(c =>
        {
            var cred = crediarios.FirstOrDefault(x => x.clienteId == c.Id);
            var rec = contasReceber.FirstOrDefault(x => x.clienteId == c.Id);
            return new
            {
                c.Id, c.Nome, c.CpfCnpj, c.Telefone, c.Celular,
                saldoCrediario = cred?.saldoCrediario ?? 0,
                saldoReceber = rec?.saldoReceber ?? 0,
                totalDevedor = (cred?.saldoCrediario ?? 0) + (rec?.saldoReceber ?? 0)
            };
        })
        .OrderByDescending(c => c.totalDevedor)
        .ToList();

        return Ok(resultado);
    }
}
