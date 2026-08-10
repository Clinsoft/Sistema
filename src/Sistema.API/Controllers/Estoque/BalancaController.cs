using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.API.Extensions;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Sistema.API.Controllers.Estoque;

/// <summary>
/// Exportação de produtos para balanças eletrônicas (Filizola, Toledo, Ramuza, Urano).
///
/// Os layouts abaixo foram obtidos por engenharia reversa de arquivos reais que
/// funcionam nos softwares de importação de cada fabricante e são reproduzidos
/// byte a byte. Cada balança importa uma PASTA com nomes de arquivo fixos, por
/// isso o retorno é um ZIP:
///   • Toledo (MGV5/6/7) e Ramuza (Atena): ITENSMGV.TXT + INFNUTRI.TXT
///   • Filizola (Smart) e Urano (Integra): CADTXT.TXT
///
/// Layout do registro de produto (largura fixa, sem separador, codificação Latin-1):
///
/// Toledo/Ramuza ITENSMGV.TXT
///   "01" + código(7) + preço-centavos(6) + validade-dias(3) + descrição(50)
///   MGV5 (153): + código(16) + "11" + zeros(50) + código(7) + zeros(8)
///   MGV6/MGV7/Ramuza (260): + código(16) + "11" + zeros(49) + código(7) + zeros(14)
///                            + "|01|" + espaços(70) + zeros(25) + "||0||"
///
/// Filizola/Urano CADTXT.TXT (165)
///   código(6) + "P" + descrição(22) + preço-centavos(7) + validade-dias(3)
///   + "@" + espaços(35) + zeros(63) + "*"×18
///   Filizola: + "000000000"      Urano: + "0,0000000"
/// </summary>
[ApiController]
[Route("api/balanca")]
[Authorize]
public class BalancaController(SistemaDbContext db) : ControllerBase
{
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(
        [FromQuery] Guid empresaId,
        [FromQuery] string modelo,
        [FromQuery] Guid? localEstoqueId,
        CancellationToken ct = default)
    {
        localEstoqueId = User.EscoparLoja(localEstoqueId);   // atendente: sempre a própria loja
        var produtos = await ObterProdutosBalancaAsync(empresaId, localEstoqueId, ct);

        var (arquivos, familia) = modelo.ToUpperInvariant() switch
        {
            "FILIZOLA" or "FILIZOLA_SMART" => (GerarFilizolaUrano(produtos, urano: false), "filizola"),
            "URANO" or "URANO_INTEGRA"     => (GerarFilizolaUrano(produtos, urano: true),  "urano"),
            "TOLEDO_MGV5"                  => (GerarToledoRamuza(produtos, mgv5: true),     "toledo_mgv5"),
            "TOLEDO_MGV6"                  => (GerarToledoRamuza(produtos, mgv5: false),    "toledo_mgv6"),
            "TOLEDO_MGV7"                  => (GerarToledoRamuza(produtos, mgv5: false),    "toledo_mgv7"),
            "RAMUZA" or "RAMUZA_ATENA"     => (GerarToledoRamuza(produtos, mgv5: false),    "ramuza"),
            _ => throw new InvalidOperationException($"Modelo '{modelo}' não suportado.")
        };

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nome, conteudo) in arquivos)
            {
                var entry = zip.CreateEntry(nome, CompressionLevel.Optimal);
                using var s = entry.Open();
                var bytes = Encoding.Latin1.GetBytes(conteudo);
                s.Write(bytes, 0, bytes.Length);
            }
        }

        // Exportou → limpa a pendência de balança dos produtos que entraram neste arquivo
        // (da loja escolhida, quando informada — senão de toda a empresa).
        await db.Produtos
            .Where(p => p.EmpresaId == empresaId && p.BalancaDesatualizada
                     && (p.ProdutoBalanca || p.VendidoFracionado)
                     && (localEstoqueId == null
                         || db.MovimentacoesEstoque.Any(m => m.ProdutoId == p.Id && m.LocalEstoqueId == localEstoqueId)))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.BalancaDesatualizada, false), ct);

        var nomeZip = $"balanca_{familia}_{DateTime.Now:yyyyMMdd_HHmm}.zip";
        return File(ms.ToArray(), "application/zip", nomeZip);
    }

    /// <summary>Quantos produtos de peso estão pendentes de envio à balança (troca de preço/novo).</summary>
    [HttpGet("pendencias")]
    public async Task<IActionResult> Pendencias(
        [FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        localEstoqueId = User.EscoparLoja(localEstoqueId);   // atendente: sempre a própria loja
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo && p.BalancaDesatualizada
                     && (p.ProdutoBalanca || p.VendidoFracionado)
                     && (localEstoqueId == null
                         || db.MovimentacoesEstoque.Any(m => m.ProdutoId == p.Id && m.LocalEstoqueId == localEstoqueId)))
            .OrderBy(p => p.Descricao)
            .Select(p => new { p.Id, p.Codigo, p.CodigoPlu, p.Descricao, p.PrecoVenda })
            .ToListAsync(ct);
        return Ok(new { pendentes = produtos.Count, produtos });
    }

    /// <summary>
    /// Produtos que vão para a balança: os marcados como Balança/Fracionado ou
    /// que usam unidade pesável (KG). O PLU é o CodigoPlu e, quando não houver,
    /// o código interno do produto — que é o número usado na balança.
    /// </summary>
    private async Task<List<ProdutoBalancaDto>> ObterProdutosBalancaAsync(
        Guid empresaId, Guid? localEstoqueId, CancellationToken ct)
    {
        // Com loja informada, só entram os produtos que têm movimentação naquele local
        // (recebidos/vendidos ali) — assim a balança de cada loja leva só os seus produtos.
        var itens = await (
            from p in db.Produtos.AsNoTracking()
            join u in db.UnidadesMedida on p.UnidadeMedidaId equals u.Id into unids
            from u in unids.DefaultIfEmpty()
            where p.EmpresaId == empresaId && p.Ativo
               && (p.ProdutoBalanca || p.VendidoFracionado || (u != null && u.Pesavel))
               && (localEstoqueId == null
                   || db.MovimentacoesEstoque.Any(m => m.ProdutoId == p.Id && m.LocalEstoqueId == localEstoqueId))
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
    public async Task<IActionResult> ProdutosParaExportar(
        [FromQuery] Guid empresaId, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
        => Ok(await ObterProdutosBalancaAsync(empresaId, User.EscoparLoja(localEstoqueId), ct));

    // ─── Toledo (MGV5/6/7) e Ramuza (Atena) ─────────────────────────────────
    private static List<(string nome, string conteudo)> GerarToledoRamuza(
        IReadOnlyList<ProdutoBalancaDto> produtos, bool mgv5)
    {
        var itens = new StringBuilder();
        var nutri = new StringBuilder();

        foreach (var p in produtos)
        {
            var cod7  = p.Plu.ToString("D7");
            var cod16 = p.Plu.ToString("D16");
            var preco = Centavos(p.PrecoVenda).ToString("D6");   // R$ 0000.00 → 6 dígitos
            var val   = (p.ValidadeEmDias ?? 0).ToString("D3");
            // MGV5 preserva acentuação (Latin-1); MGV6/7/Ramuza usam ASCII sem acento.
            var descTxt = (mgv5 ? p.Descricao : RemoverAcentos(p.Descricao)).ToUpperInvariant();
            var desc  = TruncarPadDireita(descTxt, 50);

            var prefixo = $"01{cod7}{preco}{val}{desc}";

            if (mgv5)
                itens.Append($"{prefixo}  {cod16}11{Zeros(50)}{cod7}{Zeros(8)}");
            else
                itens.Append(
                    $"{prefixo}{cod16}11{Zeros(49)}{cod7}{Zeros(14)}|01|{Espacos(70)}{Zeros(25)}||0||");
            itens.Append("\r\n");

            // INFNUTRI: uma linha por produto (dados nutricionais zerados por padrão).
            nutri.Append($"N{p.Plu:D6}{Zeros(38)}\r\n");
        }

        return
        [
            ("ITENSMGV.TXT", itens.ToString()),
            ("INFNUTRI.TXT", nutri.ToString()),
        ];
    }

    // ─── Filizola (Smart) e Urano (Integra) ─────────────────────────────────
    private static List<(string nome, string conteudo)> GerarFilizolaUrano(
        IReadOnlyList<ProdutoBalancaDto> produtos, bool urano)
    {
        var sb = new StringBuilder();
        var fim = urano ? "0,0000000" : "000000000";

        foreach (var p in produtos)
        {
            var cod6  = p.Plu.ToString("D6");
            var desc  = TruncarPadDireita(RemoverAcentos(p.Descricao).ToUpperInvariant(), 22);
            var preco = Centavos(p.PrecoVenda).ToString("D7");   // 7 dígitos
            var val   = (p.ValidadeEmDias ?? 0).ToString("D3");
            sb.Append($"{cod6}P{desc}{preco}{val}@{Espacos(35)}{Zeros(63)}{new string('*', 18)}{fim}\r\n");
        }

        return [("CADTXT.TXT", sb.ToString())];
    }

    private static long Centavos(decimal preco) => (long)Math.Round(preco * 100m, MidpointRounding.AwayFromZero);
    private static string Zeros(int n) => new('0', n);
    private static string Espacos(int n) => new(' ', n);

    private static string TruncarPadDireita(string s, int largura)
        => (s.Length > largura ? s[..largura] : s).PadRight(largura);

    private static string RemoverAcentos(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);
        foreach (var c in normalizado)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>Produto já preparado para a balança (PLU resolvido).</summary>
public record ProdutoBalancaDto(int Plu, string Descricao, decimal PrecoVenda, int? ValidadeEmDias);
