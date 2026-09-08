using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace Sistema.API.Controllers.Compras;

[ApiController]
[Route("api/cotacoes")]
[Authorize]
public class CotacoesController(SistemaDbContext db) : ControllerBase
{
    [HttpPost("comparar")]
    [RequestSizeLimit(30_000_000)]
    public IActionResult Comparar(
        [FromForm] Guid empresaId,
        [FromForm] IFormFile? pdf1,
        [FromForm] IFormFile? pdf2,
        [FromForm] IFormFile? pdf3,
        [FromForm] string? nome1,
        [FromForm] string? nome2,
        [FromForm] string? nome3,
        CancellationToken _)
    {
        var arquivos = new[] { (pdf1, nome1 ?? "Fornecedor 1"), (pdf2, nome2 ?? "Fornecedor 2"), (pdf3, nome3 ?? "Fornecedor 3") }
            .Where(x => x.Item1 is not null)
            .ToList();

        if (arquivos.Count == 0)
            return BadRequest("Envie ao menos um PDF.");

        // Carrega produtos da empresa para matching
        var produtos = db.Produtos
            .Where(p => p.EmpresaId == empresaId && p.Ativo)
            .Select(p => new { p.Id, p.Descricao, p.CodigoBarras, p.Codigo, p.CustoUnitario })
            .ToList();

        // Extrai itens de cada PDF
        var cotacoesPorFornecedor = new List<(string Fornecedor, List<ItemExtraido> Itens)>();
        foreach (var (arquivo, nomeForn) in arquivos)
        {
            var texto = ExtrairTexto(arquivo!);
            var itens = ExtrairItens(texto);
            cotacoesPorFornecedor.Add((nomeForn, itens));
        }

        // Para cada produto do catálogo, encontra o melhor item em cada fornecedor
        var resultado = new List<object>();
        var itensUsados = new HashSet<ItemExtraido>();

        foreach (var produto in produtos)
        {
            var cotacoesProduto = cotacoesPorFornecedor.Select(cf =>
            {
                var match = MelhorItem(cf.Itens, produto.Descricao, produto.CodigoBarras);
                if (match is not null) itensUsados.Add(match);
                return new
                {
                    fornecedor = cf.Fornecedor,
                    preco = match?.Preco,
                    unidade = match?.Unidade,
                    descricaoOriginal = match?.Descricao
                };
            }).ToList();

            var precos = cotacoesProduto.Where(c => c.preco.HasValue).Select(c => c.preco!.Value).ToList();
            if (!precos.Any()) continue;

            var menorPreco = precos.Min();

            resultado.Add(new
            {
                produtoId = produto.Id,
                descricao = produto.Descricao,
                custoAtual = produto.CustoUnitario,
                menorPreco,
                economia = produto.CustoUnitario > 0 ? produto.CustoUnitario - menorPreco : (decimal?)null,
                cotacoes = cotacoesProduto.Select(c => new
                {
                    c.fornecedor,
                    c.preco,
                    c.unidade,
                    c.descricaoOriginal,
                    melhor = c.preco.HasValue && c.preco.Value == menorPreco
                }).ToList()
            });
        }

        // Itens não identificados = os extraídos que não casaram com nenhum produto
        var naoIdentificados = new List<object>();
        foreach (var (fornecedor, itens) in cotacoesPorFornecedor)
            foreach (var item in itens)
                if (!itensUsados.Contains(item))
                    naoIdentificados.Add(new { fornecedor, item.Descricao, item.Preco, item.Unidade });

        return Ok(new
        {
            fornecedores = cotacoesPorFornecedor.Select(cf => cf.Fornecedor).ToList(),
            produtos = resultado.OrderBy(r => ((dynamic)r).descricao),
            naoIdentificados = naoIdentificados.Take(50),
            totalProdutos = resultado.Count,
            totalNaoIdentificados = naoIdentificados.Count
        });
    }

    /// <summary>
    /// Compara os PDFs de fornecedores usando os ITENS de uma REQUISIÇÃO como base
    /// (produto + quantidade já pedida). Para cada item aponta o fornecedor mais barato,
    /// calcula o subtotal (qtd × preço) e o total otimizado do rateio.
    /// </summary>
    [HttpPost("comparar-requisicao")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> CompararRequisicao(
        [FromForm] Guid empresaId,
        [FromForm] Guid requisicaoId,
        [FromForm] IFormFile? pdf1,
        [FromForm] IFormFile? pdf2,
        [FromForm] IFormFile? pdf3,
        [FromForm] string? nome1,
        [FromForm] string? nome2,
        [FromForm] string? nome3,
        CancellationToken ct)
    {
        var arquivos = new[]
        {
            (Arq: pdf1, Nome: nome1 ?? "Fornecedor 1"),
            (Arq: pdf2, Nome: nome2 ?? "Fornecedor 2"),
            (Arq: pdf3, Nome: nome3 ?? "Fornecedor 3"),
        }.Where(x => x.Arq is not null).ToList();

        if (arquivos.Count == 0)
            return BadRequest("Envie ao menos um PDF.");

        // Itens da requisição = base da comparação (produto + quantidade pedida)
        var itensReq = await db.ItensRequisicaoCompra.AsNoTracking()
            .Where(i => i.RequisicaoCompraId == requisicaoId)
            .Select(i => new { i.ProdutoId, i.Descricao, i.Quantidade })
            .ToListAsync(ct);

        if (itensReq.Count == 0)
            return BadRequest("A requisição não tem itens.");

        var prodIds = itensReq.Select(i => i.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Descricao, p.CodigoBarras, p.CustoUnitario })
            .ToListAsync(ct);
        var pmap = produtos.ToDictionary(p => p.Id);

        // Extrai os itens de cada PDF
        var cotacoesPorFornecedor = arquivos
            .Select(a => (Fornecedor: a.Nome, Itens: ExtrairItens(ExtrairTexto(a.Arq!))))
            .ToList();

        // Para cada item da requisição, acha o preço em cada fornecedor
        var linhas = new List<object>();
        var totaisPorForn = cotacoesPorFornecedor
            .ToDictionary(cf => cf.Fornecedor, _ => (Itens: 0, Total: 0m));

        var totalOtimizado = 0m;
        var semCotacao = 0;

        foreach (var it in itensReq)
        {
            var produto = pmap.GetValueOrDefault(it.ProdutoId);
            var desc = produto?.Descricao ?? it.Descricao;
            var ean = produto?.CodigoBarras;

            var cotacoes = cotacoesPorFornecedor.Select(cf =>
            {
                var match = MelhorItem(cf.Itens, desc, ean);

                return new
                {
                    fornecedor = cf.Fornecedor,
                    preco = match?.Preco,
                    unidade = match?.Unidade,
                    descricaoOriginal = match?.Descricao,
                    subtotal = match is not null ? match.Preco * it.Quantidade : (decimal?)null,
                };
            }).ToList();

            var comPreco = cotacoes.Where(c => c.preco.HasValue).ToList();
            if (comPreco.Count == 0) { semCotacao++; }

            var menor = comPreco.Count > 0 ? comPreco.Min(c => c.preco!.Value) : (decimal?)null;
            var melhor = menor.HasValue ? comPreco.First(c => c.preco!.Value == menor.Value) : null;

            if (melhor is not null)
            {
                totalOtimizado += melhor.subtotal!.Value;
                var acc = totaisPorForn[melhor.fornecedor];
                totaisPorForn[melhor.fornecedor] = (acc.Itens + 1, acc.Total + melhor.subtotal!.Value);
            }

            linhas.Add(new
            {
                produtoId = it.ProdutoId,
                descricao = desc,
                quantidade = it.Quantidade,
                custoAtual = produto?.CustoUnitario ?? 0m,
                melhorFornecedor = melhor?.fornecedor,
                melhorPreco = menor,
                subtotalMelhor = melhor?.subtotal,
                cotacoes = cotacoes.Select(c => new
                {
                    c.fornecedor,
                    c.preco,
                    c.unidade,
                    c.subtotal,
                    c.descricaoOriginal,
                    encontrado = c.preco.HasValue,
                    melhor = menor.HasValue && c.preco.HasValue && c.preco.Value == menor.Value,
                }).ToList(),
            });
        }

        return Ok(new
        {
            requisicaoId,
            fornecedores = cotacoesPorFornecedor.Select(cf => cf.Fornecedor).ToList(),
            itens = linhas,
            totaisPorFornecedor = totaisPorForn.Select(kv => new
            {
                fornecedor = kv.Key,
                itensAtendidos = kv.Value.Itens,
                totalRateio = kv.Value.Total,      // total dos itens em que ESTE fornecedor ganhou
            }).ToList(),
            totalOtimizado,                        // custo comprando cada item no mais barato
            totalItens = itensReq.Count,
            itensSemCotacao = semCotacao,          // itens da requisição sem preço em nenhum PDF
        });
    }

    // ── Extração de texto do PDF ─────────────────────────────────────────────
    private static string ExtrairTexto(IFormFile arquivo)
    {
        using var stream = arquivo.OpenReadStream();
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            var words = page.GetWords().ToList();
            if (!words.Any()) continue;

            // Agrupa palavras por linha (coordenada Y arredondada)
            var linhas = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                .OrderByDescending(g => g.Key)
                .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));

            foreach (var linha in linhas)
                sb.AppendLine(linha);
        }

        return sb.ToString();
    }

    // ── Parser de itens do texto extraído ────────────────────────────────────
    // Preço: número com 2 casas decimais NÃO seguido de unidade (evita pegar peso "12,50KG").
    private static readonly Regex RxPreco = new(@"R?\$?\s*(\d{1,6}[.,]\d{2})(?!\s*(?:kgs?|grs?|g|mls?|lt|un|und|unid)\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RxEan = new(@"\b\d{13}\b", RegexOptions.Compiled);
    private static readonly Regex RxUnidade = new(@"\b(UN|KG|CX|PC|LT|ML|G|GR|KIT|PAR|MT|M|L)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Conserta dígitos separados por espaço em preços prefixados por R$: "R$ 3 9,50" → "R$ 39,50".
    private static readonly Regex RxPrecoRSespacado = new(@"R\$\s*([\d][\d\s]*[.,]\s?\d{2})", RegexOptions.Compiled);
    private static readonly Regex RxCodInicio = new(@"^\s*\d{4,7}\b", RegexOptions.Compiled);
    private static readonly HashSet<string> StopDesc =
        ["POR", "DE", "R", "RS", "KG", "UND", "UN", "CX", "PC", "LT", "ML", "GR", "MT"];

    private static string LimparDesc(string linha)
    {
        var d = RxPreco.Replace(linha, "");
        d = Regex.Replace(d, @"R\$", " ");
        d = Regex.Replace(d, @"[^\w\s\-\/]", " ").Trim();
        return Regex.Replace(d, @"\s{2,}", " ").Trim();
    }

    private static bool TemDescricao(string desc)
    {
        var semCod = RxCodInicio.Replace(desc, "").Trim();
        var palavras = semCod.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Count(t => t.Length >= 3 && !StopDesc.Contains(t.ToUpperInvariant()) && t.Any(char.IsLetter));
        return palavras >= 1 && desc.Length >= 4;
    }

    private static decimal? PrecoDaLinha(string linha)
    {
        var m = RxPreco.Match(linha);
        if (!m.Success) return null;
        var s = m.Groups[1].Value.Replace('.', ',');
        if (!decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            new System.Globalization.CultureInfo("pt-BR"), out var v)) return null;
        return v is < 0.10m or > 99999m ? null : v;
    }

    /// <summary>
    /// Extrai (descrição, preço, EAN) de uma tabela em texto. Robusto a dois layouts:
    /// (a) descrição e preço na MESMA linha (ex.: BrasBol); (b) descrição numa linha e o
    /// preço na linha seguinte "Por: R$ ..." (ex.: Vida em Grãos), inclusive com dígitos
    /// separados por espaço. Linhas "De: R$ ..." (preço riscado) são ignoradas.
    /// </summary>
    private static List<ItemExtraido> ExtrairItens(string texto)
    {
        var linhas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => RxPrecoRSespacado.Replace(l, mm => "R$ " + Regex.Replace(mm.Groups[1].Value, @"\s+", "")))
            .ToArray();

        var itens = new List<ItemExtraido>();
        string? pendDesc = null, pendEan = null;

        void Emitir(string descBruta, decimal preco, string? eanConhecido)
        {
            var descricao = descBruta;
            var ean = eanConhecido ?? (RxEan.Match(descricao) is { Success: true } m ? m.Value : null);
            if (ean is not null) descricao = descricao.Replace(ean, "").Trim();
            var uni = RxUnidade.Match(descBruta);
            itens.Add(new ItemExtraido(descricao, preco, uni.Success ? uni.Value.ToUpper() : null, ean));
        }

        foreach (var linha in linhas)
        {
            var preco = PrecoDaLinha(linha);
            var desc = LimparDesc(linha);
            var temDesc = TemDescricao(desc);
            var ehDe = Regex.IsMatch(linha, @"^\s*De\b", RegexOptions.IgnoreCase)
                       && !Regex.IsMatch(linha, @"Por", RegexOptions.IgnoreCase);

            if (preco.HasValue && temDesc)            // desc + preço na mesma linha
            {
                Emitir(desc, preco.Value, null);
                pendDesc = null; pendEan = null;
            }
            else if (preco.HasValue && !temDesc)      // só preço → usa descrição pendente (ignora "De:")
            {
                if (!ehDe && pendDesc is not null)
                {
                    Emitir(pendDesc, preco.Value, pendEan);
                    pendDesc = null; pendEan = null;
                }
            }
            else if (temDesc)                          // só descrição → guarda p/ próximo preço
            {
                pendDesc = desc;
                pendEan = RxEan.Match(desc) is { Success: true } m ? m.Value : null;
            }
        }

        // Remove duplicatas: mantém o mais barato por descrição similar
        return itens
            .GroupBy(i => Normalizar(i.Descricao))
            .Select(g => g.OrderBy(i => i.Preco).First())
            .ToList();
    }

    // ── Matching por cobertura ────────────────────────────────────────────────
    // Palavras genéricas (embalagem/origem) que não ajudam a identificar o produto.
    private static readonly HashSet<string> StopMatch =
    [
        "imp", "nac", "caixa", "cx", "saco", "sc", "pacote", "pct", "pote", "kg", "kgs", "gr", "grs",
        "ml", "lt", "un", "und", "unid", "pc", "par", "premium", "organico", "natural", "importado", "nacional"
    ];

    private static HashSet<string> TokensCanon(string s) =>
        Normalizar(s).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && t.Any(char.IsLetter) && !StopMatch.Contains(t))
            .ToHashSet();

    /// <summary>
    /// Casa a descrição de um PRODUTO com a melhor descrição extraída do PDF.
    /// Score = fração dos tokens do produto presentes no item do fornecedor (cobertura);
    /// desempate pelo item mais "justo" (menos palavras sobrando) e depois pelo menor preço.
    /// Retorna null se a cobertura ficar abaixo do limiar.
    /// </summary>
    private static ItemExtraido? MelhorItem(IEnumerable<ItemExtraido> itens, string descricaoProduto,
        string? ean, double limiar = 0.6)
    {
        var pt = TokensCanon(descricaoProduto);
        if (pt.Count == 0 && string.IsNullOrEmpty(ean)) return null;

        return itens
            .Select(i =>
            {
                if (!string.IsNullOrEmpty(ean) && i.CodigoBarras == ean)
                    return (i, score: 1.0, extra: 0);
                var vt = TokensCanon(i.Descricao);
                var inter = pt.Count(t => vt.Contains(t));
                var score = pt.Count == 0 ? 0 : (double)inter / pt.Count;
                return (i, score, extra: vt.Count - inter);
            })
            .Where(x => x.score >= limiar)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.extra)
            .ThenBy(x => x.i.Preco)
            .Select(x => x.i)
            .FirstOrDefault();
    }

    private static string Normalizar(string s) =>
        s.ToLowerInvariant()
         .Normalize(NormalizationForm.FormD)
         .Where(c => c < 128)
         .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
         .ToString()
         .Replace("-", " ")
         .Replace("/", " ");
}

internal record ItemExtraido(string Descricao, decimal Preco, string? Unidade, string? CodigoBarras);
