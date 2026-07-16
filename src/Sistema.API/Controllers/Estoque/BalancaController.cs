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
    public async Task<IActionResult> Exportar(
        [FromQuery] Guid empresaId,
        [FromQuery] string modelo,
        [FromQuery] int identificadorPesavel = 2,
        [FromQuery] int tamanhoCodigoProduto = 5,
        [FromQuery] string tipoInformacao = "PrecoTotal",
        CancellationToken ct = default)
    {
        var cfg = new BalancaConfig(
            Math.Clamp(identificadorPesavel, 1, 9),
            Math.Clamp(tamanhoCodigoProduto, 4, 6),
            tipoInformacao);

        var produtos = await ObterProdutosBalancaAsync(empresaId, ct);

        var conteudo = modelo.ToUpper() switch
        {
            "FILIZOLA" or "FILIZOLA_SMART" => GerarFilizolaSmart(produtos, cfg),
            "TOLEDO_MGV5"                  => GerarToledoMgv5(produtos, cfg),
            "TOLEDO_MGV6"                  => GerarToledoMgv6(produtos, cfg),
            "TOLEDO_MGV7"                  => GerarToledoMgv7(produtos, cfg),
            "RAMUZA" or "RAMUZA_ATENA"     => GerarRamuzaAtena(produtos, cfg),
            "URANO" or "URANO_INTEGRA"     => GerarUranoIntegra(produtos, cfg),
            _ => throw new InvalidOperationException($"Modelo '{modelo}' não suportado.")
        };

        var nomeArquivo = $"balanca_{modelo.ToLower()}_{DateTime.Now:yyyyMMdd_HHmm}.txt";
        return File(Encoding.Latin1.GetBytes(conteudo), "text/plain", nomeArquivo);
    }

    /// <summary>
    /// Produtos que vão para a balança: os marcados como Balança/Fracionado ou
    /// que usam unidade pesável (KG). O PLU é o CodigoPlu e, quando não houver,
    /// o código interno do produto — que é o número usado na balança.
    /// </summary>
    private async Task<List<ProdutoBalancaDto>> ObterProdutosBalancaAsync(Guid empresaId, CancellationToken ct)
    {
        var itens = await (
            from p in db.Produtos.AsNoTracking()
            join u in db.UnidadesMedida on p.UnidadeMedidaId equals u.Id into unids
            from u in unids.DefaultIfEmpty()
            where p.EmpresaId == empresaId && p.Ativo
               && (p.ProdutoBalanca || p.VendidoFracionado || (u != null && u.Pesavel))
            select new { p.Codigo, p.CodigoPlu, p.Descricao, p.PrecoVenda, p.ValidadeEmDias }
        ).ToListAsync(ct);

        return itens
            .Select(i => new ProdutoBalancaDto(
                Plu: i.CodigoPlu ?? (int.TryParse(i.Codigo, out var c) ? c : 0),
                Descricao: i.Descricao,
                PrecoVenda: i.PrecoVenda,
                ValidadeEmDias: i.ValidadeEmDias))
            .Where(i => i.Plu > 0)      // sem PLU válido a balança não identifica o produto
            .OrderBy(i => i.Plu)
            .ToList();
    }

    /// <summary>Prévia dos produtos que serão exportados (usada pela tela).</summary>
    [HttpGet("produtos")]
    public async Task<IActionResult> ProdutosParaExportar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await ObterProdutosBalancaAsync(empresaId, ct));

    // ─── Filizola SMART ──────────────────────────────────────────────────────
    // Formato: PLU|DESCRICAO|PRECO|VALIDADE_DIAS
    private static string GerarFilizolaSmart(
        IEnumerable<ProdutoBalancaDto> produtos, BalancaConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"#CONFIG IP={cfg.IdentificadorPesavel} TC={cfg.TamanhoCodigoProduto} TI={cfg.TipoInformacao}");
        foreach (var p in produtos)
        {
            var plu = p.Plu.ToString($"D{cfg.TamanhoCodigoProduto}");
            var desc = p.Descricao.Length > 22 ? p.Descricao[..22] : p.Descricao.PadRight(22);
            sb.AppendLine($"{plu}|{desc}|{p.PrecoVenda:F2}|{p.ValidadeEmDias ?? 0}");
        }
        return sb.ToString();
    }

    // ─── Toledo MGV5 ─────────────────────────────────────────────────────────
    // Formato fixo: PLU (N dígitos), descrição (22 chars), preço (7 dígitos sem separador), validade (3 dígitos)
    private static string GerarToledoMgv5(
        IEnumerable<ProdutoBalancaDto> produtos, BalancaConfig cfg)
    {
        var sb = new StringBuilder();
        foreach (var p in produtos)
        {
            var plu  = p.Plu.ToString($"D{cfg.TamanhoCodigoProduto}");
            var desc = (p.Descricao.Length > 22 ? p.Descricao[..22] : p.Descricao).PadRight(22);
            var preco    = ((long)(p.PrecoVenda * 100)).ToString("D7");
            var validade = (p.ValidadeEmDias ?? 0).ToString("D3");
            sb.AppendLine($"{plu}{desc}{preco}{validade}");
        }
        return sb.ToString();
    }

    // ─── Toledo MGV6 ─────────────────────────────────────────────────────────
    private static string GerarToledoMgv6(
        IEnumerable<ProdutoBalancaDto> produtos, BalancaConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"01CABECALHO IP={cfg.IdentificadorPesavel} TC={cfg.TamanhoCodigoProduto} TI={cfg.TipoInformacao}");
        foreach (var p in produtos)
        {
            var plu      = p.Plu.ToString($"D{cfg.TamanhoCodigoProduto}");
            var desc     = (p.Descricao.Length > 30 ? p.Descricao[..30] : p.Descricao).PadRight(30);
            var preco    = $"{p.PrecoVenda:F2}".Replace(",", "").PadLeft(8, '0');
            var validade = (p.ValidadeEmDias ?? 0).ToString("D4");
            sb.AppendLine($"02{plu}{desc}{preco}{validade}");
        }
        sb.AppendLine("99FIM");
        return sb.ToString();
    }

    // ─── Toledo MGV7 ─────────────────────────────────────────────────────────
    private static string GerarToledoMgv7(
        IEnumerable<ProdutoBalancaDto> produtos, BalancaConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Configuracao]");
        sb.AppendLine($"IdentificadorPesavel={cfg.IdentificadorPesavel}");
        sb.AppendLine($"TamanhoCodigoProduto={cfg.TamanhoCodigoProduto}");
        sb.AppendLine($"TipoInformacao={cfg.TipoInformacao}");
        sb.AppendLine();
        foreach (var p in produtos)
        {
            var preco = p.PrecoVenda.ToString("F2").Replace(",", ".");
            sb.AppendLine("[Produto]");
            sb.AppendLine($"PLU={p.Plu.ToString($"D{cfg.TamanhoCodigoProduto}")}");
            sb.AppendLine($"Nome={p.Descricao[..Math.Min(p.Descricao.Length, 30)]}");
            sb.AppendLine($"Preco={preco}");
            sb.AppendLine($"Validade={p.ValidadeEmDias ?? 0}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ─── Ramuza Atena ────────────────────────────────────────────────────────
    private static string GerarRamuzaAtena(
        IEnumerable<ProdutoBalancaDto> produtos, BalancaConfig cfg)
    {
        var sb = new StringBuilder();
        foreach (var p in produtos)
        {
            var plu      = p.Plu.ToString($"D{cfg.TamanhoCodigoProduto}");
            var desc     = (p.Descricao.Length > 22 ? p.Descricao[..22] : p.Descricao).PadRight(22);
            var preco    = ((long)(p.PrecoVenda * 100)).ToString("D6");
            var validade = (p.ValidadeEmDias ?? 0).ToString("D3");
            sb.AppendLine($"{plu}{desc}{preco}{validade}");
        }
        return sb.ToString();
    }

    // ─── Urano Integra ───────────────────────────────────────────────────────
    private static string GerarUranoIntegra(
        IEnumerable<ProdutoBalancaDto> produtos, BalancaConfig cfg)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"PRODUTOS IP={cfg.IdentificadorPesavel};TC={cfg.TamanhoCodigoProduto};TI={cfg.TipoInformacao}");
        foreach (var p in produtos)
        {
            var plu   = p.Plu.ToString($"D{cfg.TamanhoCodigoProduto}");
            var preco = p.PrecoVenda.ToString("F2").Replace(",", ".");
            sb.AppendLine($"{plu};{p.Descricao[..Math.Min(p.Descricao.Length, 25)]};{preco};{p.ValidadeEmDias ?? 0}");
        }
        return sb.ToString();
    }
}

public record BalancaConfig(int IdentificadorPesavel, int TamanhoCodigoProduto, string TipoInformacao);

/// <summary>Produto já preparado para a balança (PLU resolvido).</summary>
public record ProdutoBalancaDto(int Plu, string Descricao, decimal PrecoVenda, int? ValidadeEmDias);
