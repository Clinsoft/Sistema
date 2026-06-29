using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        // Para cada produto, encontra preços em cada fornecedor
        var resultado = new List<object>();
        var produtosVistos = new HashSet<Guid>();

        foreach (var (fornecedor, itens) in cotacoesPorFornecedor)
        {
            foreach (var item in itens)
            {
                // Match por código de barras primeiro, depois por nome fuzzy
                var produto = produtos.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(item.CodigoBarras) && p.CodigoBarras == item.CodigoBarras)
                    ?? MatchFuzzy(produtos.Select(p => new { p.Id, p.Descricao, p.CodigoBarras, p.Codigo, p.CustoUnitario }).ToList(), item.Descricao);

                if (produto is not null && !produtosVistos.Contains(produto.Id))
                    produtosVistos.Add(produto.Id);
            }
        }

        // Constrói tabela de comparação por produto encontrado
        foreach (var produtoId in produtosVistos)
        {
            var produto = produtos.First(p => p.Id == produtoId);
            var cotacoesProduto = cotacoesPorFornecedor.Select(cf =>
            {
                var match = cf.Itens
                    .Where(i => (!string.IsNullOrEmpty(i.CodigoBarras) && i.CodigoBarras == produto.CodigoBarras)
                        || ScoreFuzzy(Normalizar(produto.Descricao), Normalizar(i.Descricao)) >= 0.5)
                    .OrderByDescending(i => ScoreFuzzy(Normalizar(produto.Descricao), Normalizar(i.Descricao)))
                    .FirstOrDefault();

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

        // Itens não identificados (sem match no banco)
        var naoIdentificados = new List<object>();
        foreach (var (fornecedor, itens) in cotacoesPorFornecedor)
        {
            foreach (var item in itens)
            {
                var produto = produtos.FirstOrDefault(p =>
                    (!string.IsNullOrEmpty(item.CodigoBarras) && p.CodigoBarras == item.CodigoBarras)
                    || ScoreFuzzy(Normalizar(p.Descricao), Normalizar(item.Descricao)) >= 0.5);

                if (produto is null)
                    naoIdentificados.Add(new { fornecedor, item.Descricao, item.Preco, item.Unidade });
            }
        }

        return Ok(new
        {
            fornecedores = cotacoesPorFornecedor.Select(cf => cf.Fornecedor).ToList(),
            produtos = resultado.OrderBy(r => ((dynamic)r).descricao),
            naoIdentificados = naoIdentificados.Take(50),
            totalProdutos = resultado.Count,
            totalNaoIdentificados = naoIdentificados.Count
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
    private static readonly Regex RxPreco = new(@"R?\$?\s*(\d{1,6}[.,]\d{2})", RegexOptions.Compiled);
    private static readonly Regex RxEan = new(@"\b\d{13}\b", RegexOptions.Compiled);
    private static readonly Regex RxUnidade = new(@"\b(UN|KG|CX|PC|LT|ML|G|GR|KIT|PAR|MT|M|L)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static List<ItemExtraido> ExtrairItens(string texto)
    {
        var itens = new List<ItemExtraido>();
        var linhas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var linha in linhas)
        {
            var precoMatch = RxPreco.Match(linha);
            if (!precoMatch.Success) continue;

            var precoStr = precoMatch.Groups[1].Value.Replace('.', ',');
            if (!decimal.TryParse(precoStr, System.Globalization.NumberStyles.Any,
                new System.Globalization.CultureInfo("pt-BR"), out var preco))
                continue;

            // Ignora valores muito pequenos (centavos soltos) ou muito grandes
            if (preco < 0.10m || preco > 99999m) continue;

            var descricao = RxPreco.Replace(linha, "").Trim();
            descricao = Regex.Replace(descricao, @"[^\w\s\-\/]", " ").Trim();
            descricao = Regex.Replace(descricao, @"\s{2,}", " ").Trim();

            if (descricao.Length < 3) continue;

            var ean = RxEan.Match(descricao);
            var codigoBarras = ean.Success ? ean.Value : null;
            if (codigoBarras is not null)
                descricao = descricao.Replace(codigoBarras, "").Trim();

            var unidadeMatch = RxUnidade.Match(linha);
            var unidade = unidadeMatch.Success ? unidadeMatch.Value.ToUpper() : null;

            itens.Add(new ItemExtraido(descricao, preco, unidade, codigoBarras));
        }

        // Remove duplicatas: mantém o mais barato por descrição similar
        return itens
            .GroupBy(i => Normalizar(i.Descricao))
            .Select(g => g.OrderBy(i => i.Preco).First())
            .ToList();
    }

    // ── Matching fuzzy ───────────────────────────────────────────────────────
    private static T? MatchFuzzy<T>(List<T> produtos, string descricao) where T : class
    {
        var normalizado = Normalizar(descricao);
        return produtos
            .Select(p => (p, score: ScoreFuzzy(Normalizar(ObterDescricao(p)), normalizado)))
            .Where(x => x.score >= 0.5)
            .OrderByDescending(x => x.score)
            .Select(x => x.p)
            .FirstOrDefault();
    }

    private static double ScoreFuzzy(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
        var tokensA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var tokensB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var intersecao = tokensA.Intersect(tokensB).Count();
        var maxTokens = Math.Max(tokensA.Count, tokensB.Count);
        return maxTokens == 0 ? 0 : (double)intersecao / maxTokens;
    }

    private static string Normalizar(string s) =>
        s.ToLowerInvariant()
         .Normalize(NormalizationForm.FormD)
         .Where(c => c < 128)
         .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
         .ToString()
         .Replace("-", " ")
         .Replace("/", " ");

    private static string ObterDescricao<T>(T obj)
    {
        var prop = typeof(T).GetProperty("Descricao");
        return prop?.GetValue(obj)?.ToString() ?? "";
    }
}

internal record ItemExtraido(string Descricao, decimal Preco, string? Unidade, string? CodigoBarras);
