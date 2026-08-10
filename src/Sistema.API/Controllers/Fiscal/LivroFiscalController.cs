using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Fiscal;

/// <summary>Livro Fiscal — entradas e saídas consolidadas para o contador.</summary>
[ApiController]
[Route("api/fiscal/livro")]
[Authorize(Roles = "Administrador,Financeiro,Contador")]
public class LivroFiscalController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Entradas de produtos por fornecedor (NF de compra recebida).</summary>
    [HttpGet("entradas")]
    public async Task<IActionResult> Entradas([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        // Entradas = movimentações do tipo Entrada com documentoOrigem
        var movs = await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId
                && m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Entrada
                && m.CriadoEm >= inicio && m.CriadoEm < fim.AddDays(1)
                && m.DocumentoOrigem != null)
            .Join(db.Produtos, m => m.ProdutoId, p => p.Id, (m, p) => new
            {
                m.Id, m.ProdutoId, produtoNome = p.Descricao, p.Ncm, p.Cfop,
                m.Quantidade, m.CustoUnitario,
                total = m.Quantidade * m.CustoUnitario,
                m.DocumentoOrigem, m.CriadoEm
            })
            .OrderBy(m => m.CriadoEm)
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            totalEntradas = movs.Sum(m => m.total),
            qtdMovimentacoes = movs.Count,
            entradas = movs
        });
    }

    /// <summary>Saídas (vendas) com detalhamento fiscal.</summary>
    [HttpGet("saidas")]
    public async Task<IActionResult> Saidas([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var notas = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao < fim.AddDays(1)
                && n.Status == StatusNF.Autorizada)
            .Include(n => n.Itens)
            .OrderBy(n => n.DataEmissao)
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            totalSaidas = notas.Sum(n => n.TotalNota),
            totalIcms = notas.Sum(n => n.TotalIcms),
            totalPis = notas.Sum(n => n.TotalPis),
            totalCofins = notas.Sum(n => n.TotalCofins),
            qtdNotas = notas.Count,
            saidas = notas.Select(n => new
            {
                n.Id, n.Modelo, n.Serie, n.Numero, n.ChaveAcesso,
                n.DataEmissao, n.NomeDestinatario,
                n.TotalProdutos, n.TotalDesconto, n.TotalIcms, n.TotalNota,
                qtdItens = n.Itens.Count
            })
        });
    }

    /// <summary>Espelho fiscal — entradas e saídas consolidadas (para contador).</summary>
    [HttpGet("espelho")]
    public async Task<IActionResult> Espelho([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var entradas = await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId
                && m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Entrada
                && m.CriadoEm >= inicio && m.CriadoEm < fim.AddDays(1))
            .SumAsync(m => (decimal?)(m.Quantidade * m.CustoUnitario) ?? 0, ct);

        var saidas = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao < fim.AddDays(1)
                && n.Status == StatusNF.Autorizada)
            .SumAsync(n => (decimal?)n.TotalNota ?? 0, ct);

        var icmsSaidas = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao < fim.AddDays(1)
                && n.Status == StatusNF.Autorizada)
            .SumAsync(n => (decimal?)n.TotalIcms ?? 0, ct);

        var pisSaidas = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao < fim.AddDays(1)
                && n.Status == StatusNF.Autorizada)
            .SumAsync(n => (decimal?)n.TotalPis ?? 0, ct);

        var cofinsSaidas = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao < fim.AddDays(1)
                && n.Status == StatusNF.Autorizada)
            .SumAsync(n => (decimal?)n.TotalCofins ?? 0, ct);

        return Ok(new
        {
            periodo = new { ano, mes, inicio, fim },
            entradas = new { total = entradas },
            saidas = new
            {
                total = saidas,
                icms = icmsSaidas,
                pis = pisSaidas,
                cofins = cofinsSaidas,
                totalImpostos = icmsSaidas + pisSaidas + cofinsSaidas
            },
            resultado = saidas - entradas
        });
    }
}
