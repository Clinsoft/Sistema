using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;
using System.Text;
using System.Text.Json;

namespace Sistema.API.Controllers.Estoque;

/// <summary>Exportação de dados para integração com e-commerce (Magento, Moovin).</summary>
[ApiController]
[Route("api/exportacao")]
[Authorize]
public class ExportacaoController(SistemaDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>Exporta produtos para importação no Magento (CSV).</summary>
    [HttpGet("magento")]
    public async Task<IActionResult> Magento([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo)
            .Join(db.Categorias, p => p.CategoriaId, c => c.Id, (p, c) => new { p, CategoriaNome = c.Nome })
            .Join(db.Marcas, x => x.p.MarcaId, m => m.Id, (x, m) => new { x.p, x.CategoriaNome, MarcaNome = m.Nome })
            .OrderBy(x => x.p.Codigo)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        // Cabeçalho Magento
        sb.AppendLine("sku,name,price,special_price,qty,status,weight,description,short_description," +
            "category_ids,brand,barcode,visibility,type_id");

        foreach (var x in produtos)
        {
            var sku = x.p.CodigoBarras ?? x.p.Codigo;
            var nome = x.p.Descricao.Replace(",", " ").Replace("\"", "'");
            var preco = x.p.PrecoVenda.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var precoPromo = x.p.PrecoAtacado > 0
                ? x.p.PrecoAtacado.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                : "";
            var estoque = (int)x.p.EstoqueAtual;
            var status = x.p.Ativo ? "1" : "2";
            var peso = 0.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

            sb.AppendLine($"{sku},\"{nome}\",{preco},{precoPromo},{estoque},{status},{peso}," +
                $"\"{nome}\",\"{nome}\",{x.CategoriaNome},{x.MarcaNome},{x.p.CodigoBarras},4,simple");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"magento_produtos_{DateTime.Now:yyyyMMdd}.csv");
    }

    /// <summary>Exporta produtos para Moovin (JSON).</summary>
    [HttpGet("moovin")]
    public async Task<IActionResult> Moovin([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo)
            .Join(db.Categorias, p => p.CategoriaId, c => c.Id, (p, c) => new { p, CategoriaNome = c.Nome })
            .Select(x => new
            {
                sku = x.p.CodigoBarras ?? x.p.Codigo,
                name = x.p.Descricao,
                price = x.p.PrecoVenda,
                sale_price = x.p.PrecoAtacado > 0 ? (decimal?)x.p.PrecoAtacado : null,
                stock = (int)x.p.EstoqueAtual,
                category = x.CategoriaNome,
                ean = x.p.CodigoBarras,
                weight = 0,
                active = x.p.Ativo
            })
            .ToListAsync(ct);

        var json = JsonSerializer.Serialize(new { products = produtos }, JsonOpts);
        return File(Encoding.UTF8.GetBytes(json), "application/json",
            $"moovin_produtos_{DateTime.Now:yyyyMMdd}.json");
    }
}
