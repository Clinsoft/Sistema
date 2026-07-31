using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Infrastructure.Data;
using System.IO.Compression;
using System.Text;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/relatorios")]
[Authorize(Roles = "Administrador,Financeiro,Contador")]
public class RelatoriosFiscaisController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Notas fiscais por período e CFOP.</summary>
    [HttpGet("notas-por-periodo")]
    public async Task<IActionResult> NotasPorPeriodo([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] string? modelo, [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao < fim.AddDays(1));

        if (!string.IsNullOrEmpty(modelo) && Enum.TryParse<ModeloNF>(modelo, out var m))
            query = query.Where(n => n.Modelo == m);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusNF>(status, out var s))
            query = query.Where(n => n.Status == s);

        var notas = await query
            .OrderBy(n => n.DataEmissao)
            .Select(n => new
            {
                n.Id, n.Modelo, n.Serie, n.Numero, n.ChaveAcesso,
                n.DataEmissao, n.Status, n.NomeDestinatario,
                n.TotalProdutos, n.TotalDesconto, n.TotalIcms, n.TotalNota
            })
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { inicio, fim },
            qtd = notas.Count,
            totais = new
            {
                faturamento = notas.Sum(n => n.TotalNota),
                icms = notas.Sum(n => n.TotalIcms),
                descontos = notas.Sum(n => n.TotalDesconto)
            },
            notas
        });
    }

    /// <summary>Impostos consolidados por período.</summary>
    [HttpGet("impostos")]
    public async Task<IActionResult> Impostos([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var itens = await db.ItensNotaFiscal.AsNoTracking()
            .Join(db.NotasFiscais, i => i.NotaFiscalId, n => n.Id, (i, n) => new { i, n })
            .Where(x => x.n.EmpresaId == empresaId
                && x.n.DataEmissao >= inicio && x.n.DataEmissao < fim.AddDays(1)
                && x.n.Status == StatusNF.Autorizada)
            .Select(x => new
            {
                x.i.ValorTotal, x.i.ValorIcms, x.i.ValorPis, x.i.ValorCofins,
                x.i.AliquotaIcms, x.i.CstIcms, x.i.CsosnIcms
            })
            .ToListAsync(ct);

        return Ok(new
        {
            periodo = new { ano, mes },
            baseCalculo = itens.Sum(i => i.ValorTotal),
            totalIcms = itens.Sum(i => i.ValorIcms),
            totalPis = itens.Sum(i => i.ValorPis),
            totalCofins = itens.Sum(i => i.ValorCofins),
            porCst = itens
                .GroupBy(i => i.CstIcms ?? i.CsosnIcms ?? "N/A")
                .Select(g => new
                {
                    cst = g.Key,
                    base_ = g.Sum(i => i.ValorTotal),
                    icms = g.Sum(i => i.ValorIcms)
                })
        });
    }

    /// <summary>Download de XMLs autorizados do período (ZIP).</summary>
    [HttpGet("download-xml")]
    public async Task<IActionResult> DownloadXml([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var notas = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao < fim.AddDays(1)
                && n.Status == StatusNF.Autorizada
                && n.XmlRetorno != null)
            .Select(n => new { n.Numero, n.ChaveAcesso, n.XmlRetorno })
            .ToListAsync(ct);

        if (!notas.Any())
            return NotFound("Nenhuma nota autorizada encontrada no período.");

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var nota in notas)
            {
                var nomeArquivo = $"{nota.ChaveAcesso ?? nota.Numero.ToString()}-nfe.xml";
                var entry = zip.CreateEntry(nomeArquivo);
                await using var entryStream = entry.Open();
                var xmlBytes = Encoding.UTF8.GetBytes(nota.XmlRetorno ?? "");
                await entryStream.WriteAsync(xmlBytes, ct);
            }
        }

        ms.Seek(0, SeekOrigin.Begin);
        return File(ms.ToArray(), "application/zip",
            $"xmls_nfe_{inicio:yyyyMMdd}_{fim:yyyyMMdd}.zip");
    }

    /// <summary>Produtos com NCM inválido (nulo ou fora de 8 dígitos).</summary>
    [HttpGet("produtos-ncm-invalido")]
    public async Task<IActionResult> ProdutosNcmInvalido([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo
                && (p.Ncm == null || p.Ncm.Length != 8))
            .Select(p => new { p.Id, p.Codigo, p.Descricao, p.Ncm })
            .OrderBy(p => p.Descricao)
            .ToListAsync(ct);

        return Ok(new { total = produtos.Count, produtos });
    }
}
