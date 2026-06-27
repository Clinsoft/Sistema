using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;
using System.Text;

namespace Sistema.API.Controllers.Estoque;

/// <summary>Exportação de produtos para balanças eletrônicas (Filizola, Toledo, Ramuza, Urano).</summary>
[ApiController]
[Route("api/balanca")]
[Authorize]
public class BalancaController(SistemaDbContext db) : ControllerBase
{
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar([FromQuery] Guid empresaId,
        [FromQuery] string modelo, CancellationToken ct)
    {
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo && p.ProdutoBalanca && p.CodigoPlu != null)
            .OrderBy(p => p.CodigoPlu)
            .ToListAsync(ct);

        var conteudo = modelo.ToUpper() switch
        {
            "FILIZOLA" or "FILIZOLA_SMART" => GerarFilizolaSmart(produtos),
            "TOLEDO_MGV5" => GerarToledoMgv5(produtos),
            "TOLEDO_MGV6" => GerarToledoMgv6(produtos),
            "TOLEDO_MGV7" => GerarToledoMgv7(produtos),
            "RAMUZA" or "RAMUZA_ATENA" => GerarRamuzaAtena(produtos),
            "URANO" or "URANO_INTEGRA" => GerarUranoIntegra(produtos),
            _ => throw new InvalidOperationException($"Modelo '{modelo}' não suportado.")
        };

        var nomeArquivo = $"balanca_{modelo.ToLower()}_{DateTime.Now:yyyyMMdd_HHmm}.txt";
        return File(Encoding.Latin1.GetBytes(conteudo), "text/plain", nomeArquivo);
    }

    // ─── Filizola SMART ──────────────────────────────────────────────────────
    // Formato: PLU|DESCRICAO|PRECO|VALIDADE_DIAS
    private static string GerarFilizolaSmart(IEnumerable<Domain.Estoque.Entities.Produto> produtos)
    {
        var sb = new StringBuilder();
        foreach (var p in produtos)
        {
            var desc = p.Descricao.Length > 22 ? p.Descricao[..22] : p.Descricao.PadRight(22);
            sb.AppendLine($"{p.CodigoPlu:D6}|{desc}|{p.PrecoVenda:F2}|0");
        }
        return sb.ToString();
    }

    // ─── Toledo MGV5 ─────────────────────────────────────────────────────────
    // Formato fixo: posição 1-6 PLU, 7-28 descrição, 29-37 preço sem ponto, 38-40 validade dias
    private static string GerarToledoMgv5(IEnumerable<Domain.Estoque.Entities.Produto> produtos)
    {
        var sb = new StringBuilder();
        foreach (var p in produtos)
        {
            var plu = p.CodigoPlu?.ToString("D6") ?? "000000";
            var desc = (p.Descricao.Length > 22 ? p.Descricao[..22] : p.Descricao).PadRight(22);
            var preco = ((long)(p.PrecoVenda * 100)).ToString("D7");
            sb.AppendLine($"{plu}{desc}{preco}000");
        }
        return sb.ToString();
    }

    // ─── Toledo MGV6 ─────────────────────────────────────────────────────────
    private static string GerarToledoMgv6(IEnumerable<Domain.Estoque.Entities.Produto> produtos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("01CABECALHO");
        foreach (var p in produtos)
        {
            var plu = p.CodigoPlu?.ToString("D6") ?? "000000";
            var desc = (p.Descricao.Length > 30 ? p.Descricao[..30] : p.Descricao).PadRight(30);
            var preco = $"{p.PrecoVenda:F2}".Replace(",", "").PadLeft(8, '0');
            sb.AppendLine($"02{plu}{desc}{preco}0000");
        }
        sb.AppendLine("99FIM");
        return sb.ToString();
    }

    // ─── Toledo MGV7 ─────────────────────────────────────────────────────────
    private static string GerarToledoMgv7(IEnumerable<Domain.Estoque.Entities.Produto> produtos)
    {
        var sb = new StringBuilder();
        foreach (var p in produtos)
        {
            var preco = p.PrecoVenda.ToString("F2").Replace(",", ".");
            sb.AppendLine($"[Produto]");
            sb.AppendLine($"PLU={p.CodigoPlu}");
            sb.AppendLine($"Nome={p.Descricao[..Math.Min(p.Descricao.Length, 30)]}");
            sb.AppendLine($"Preco={preco}");
            sb.AppendLine($"Validade=0");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ─── Ramuza Atena ────────────────────────────────────────────────────────
    private static string GerarRamuzaAtena(IEnumerable<Domain.Estoque.Entities.Produto> produtos)
    {
        var sb = new StringBuilder();
        foreach (var p in produtos)
        {
            var plu = p.CodigoPlu?.ToString("D5") ?? "00000";
            var desc = (p.Descricao.Length > 22 ? p.Descricao[..22] : p.Descricao).PadRight(22);
            var preco = ((long)(p.PrecoVenda * 100)).ToString("D6");
            sb.AppendLine($"{plu}{desc}{preco}");
        }
        return sb.ToString();
    }

    // ─── Urano Integra ───────────────────────────────────────────────────────
    private static string GerarUranoIntegra(IEnumerable<Domain.Estoque.Entities.Produto> produtos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PRODUTOS");
        foreach (var p in produtos)
        {
            var preco = p.PrecoVenda.ToString("F2").Replace(",", ".");
            sb.AppendLine($"{p.CodigoPlu};{p.Descricao[..Math.Min(p.Descricao.Length, 25)]};{preco};0");
        }
        return sb.ToString();
    }
}
