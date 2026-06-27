using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;
using System.IO.Compression;
using System.Text;

namespace Sistema.API.Controllers.Contabilidade;

/// <summary>Download de XMLs de NF-e e NFC-e em lote por competência (ano/mês).</summary>
[ApiController]
[Route("api/contabilidade/xml")]
[Authorize]
public class DownloadXmlController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Lista os meses/anos disponíveis para download de XML.</summary>
    [HttpGet("competencias")]
    public async Task<IActionResult> Competencias([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var competencias = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.Status != Domain.Fiscal.Entities.StatusNF.Cancelada)
            .GroupBy(n => new { n.DataEmissao.Year, n.DataEmissao.Month })
            .Select(g => new
            {
                ano = g.Key.Year,
                mes = g.Key.Month,
                qtdNFe   = g.Count(n => n.Modelo == Domain.Fiscal.Entities.ModeloNF.NFe),
                qtdNFCe  = g.Count(n => n.Modelo == Domain.Fiscal.Entities.ModeloNF.NFCe),
                total    = g.Count()
            })
            .OrderByDescending(x => x.ano).ThenByDescending(x => x.mes)
            .ToListAsync(ct);

        return Ok(competencias);
    }

    /// <summary>Baixa um ZIP com todos os XMLs do mês/ano informado.</summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download(
        [FromQuery] Guid empresaId,
        [FromQuery] int ano,
        [FromQuery] int mes,
        [FromQuery] string? modelo, // "NFe", "NFCe" ou null para todos
        CancellationToken ct)
    {
        var query = db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao.Year == ano
                && n.DataEmissao.Month == mes
                && n.Status != Domain.Fiscal.Entities.StatusNF.Cancelada
                && n.ChaveAcesso != null);

        if (modelo == "NFe")
            query = query.Where(n => n.Modelo == Domain.Fiscal.Entities.ModeloNF.NFe);
        else if (modelo == "NFCe")
            query = query.Where(n => n.Modelo == Domain.Fiscal.Entities.ModeloNF.NFCe);

        var notas = await query
            .Select(n => new { n.ChaveAcesso, n.Numero, n.Modelo, n.DataEmissao })
            .ToListAsync(ct);

        if (!notas.Any())
            return NotFound($"Nenhum XML encontrado para {mes:D2}/{ano}.");

        // Monta ZIP em memória
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // Pasta raiz: CNPJ-AAAAMM
            var empresa = await db.Empresas.AsNoTracking()
                .Where(e => e.Id == empresaId)
                .Select(e => new { e.Cnpj })
                .FirstOrDefaultAsync(ct);

            var cnpj = empresa?.Cnpj ?? "00000000000000";
            var pasta = $"{cnpj}_{ano}{mes:D2}";

            foreach (var nota in notas)
            {
                var chave = nota.ChaveAcesso!;
                var xmlPath = Path.Combine("wwwroot", "uploads", "xmls", $"{chave}.xml");

                string conteudo;
                if (System.IO.File.Exists(xmlPath))
                {
                    conteudo = await System.IO.File.ReadAllTextAsync(xmlPath, ct);
                }
                else
                {
                    // XML placeholder para quando não houver arquivo armazenado
                    var tipoTag = nota.Modelo == Domain.Fiscal.Entities.ModeloNF.NFCe ? "nfce" : "nfe";
                    conteudo = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                               $"<!-- XML da {tipoTag.ToUpper()} {nota.Numero} emitida em {nota.DataEmissao:dd/MM/yyyy} -->" +
                               $"<!-- Chave de acesso: {chave} -->";
                }

                var modelo65 = nota.Modelo == Domain.Fiscal.Entities.ModeloNF.NFCe ? "NFCe" : "NFe";
                var nomeArquivo = $"{pasta}/{modelo65}/{chave}.xml";
                var entry = zip.CreateEntry(nomeArquivo, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                await writer.WriteAsync(conteudo);
            }
        }

        ms.Position = 0;
        var mesFormatado = new System.Globalization.CultureInfo("pt-BR")
            .DateTimeFormat.GetMonthName(mes);
        var nomeZip = $"XMLs_{mesFormatado}_{ano}.zip";

        return File(ms.ToArray(), "application/zip", nomeZip);
    }

    /// <summary>Resumo de entradas e saídas do período para o painel do contador.</summary>
    [HttpGet("resumo-fiscal")]
    public async Task<IActionResult> ResumoFiscal(
        [FromQuery] Guid empresaId,
        [FromQuery] int ano,
        [FromQuery] int mes,
        CancellationToken ct)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddTicks(-1);

        var saidas = await db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao <= fim
                && n.Status != Domain.Fiscal.Entities.StatusNF.Cancelada)
            .GroupBy(n => n.Modelo)
            .Select(g => new
            {
                modelo = g.Key.ToString(),
                qtd = g.Count(),
                totalProdutos = g.Sum(n => n.TotalProdutos),
                totalIcms     = g.Sum(n => n.TotalIcms),
                totalPis      = g.Sum(n => n.TotalPis),
                totalCofins   = g.Sum(n => n.TotalCofins),
            })
            .ToListAsync(ct);

        var entradas = await db.EntradasNFe.AsNoTracking()
            .Where(e => e.EmpresaId == empresaId
                && e.DataEmissao >= inicio && e.DataEmissao <= fim)
            .GroupBy(e => e.EmpresaId)
            .Select(g => new
            {
                qtd = g.Count(),
                totalProdutos = g.Sum(e => e.ValorTotal),
            })
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            competencia = new { ano, mes },
            saidas,
            entradas = entradas ?? new { qtd = 0, totalProdutos = 0m },
        });
    }
}
