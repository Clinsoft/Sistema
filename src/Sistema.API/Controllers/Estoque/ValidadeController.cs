using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/validade")]
[Authorize]
public partial class ValidadeController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    // ─── Painel ──────────────────────────────────────────────────────────────

    [HttpGet("painel")]
    public async Task<IActionResult> Painel([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var cfg = await CarregarConfig(empresaId, ct);
        var hoje = DateTime.Today;

        var itens = await (
            from l in db.Lotes
            join p in db.Produtos  on l.ProdutoId    equals p.Id
            join c in db.Categorias on p.CategoriaId equals c.Id into cats
            from c in cats.DefaultIfEmpty()
            join m in db.Marcas on p.MarcaId equals m.Id into marcas
            from m in marcas.DefaultIfEmpty()
            join u in db.UnidadesMedida on p.UnidadeMedidaId equals u.Id into unids
            from u in unids.DefaultIfEmpty()
            where l.EmpresaId == empresaId
               && l.DataValidade.HasValue
               && l.Quantidade > 0
               && p.Ativo
            orderby l.DataValidade
            select new
            {
                LoteId       = l.Id,
                ProdutoId    = p.Id,
                p.Descricao,
                p.DescricaoComplementar,
                p.Codigo,
                p.CodigoBarras,
                p.CodigoPlu,
                p.ImagemUrl,
                Marca        = m != null ? m.Nome : "",
                Categoria    = c != null ? c.Nome : "",
                l.NumeroLote,
                l.DataValidade,
                l.Quantidade,
                LoteImagemUrl = l.ImagemUrl,
                ValorEstoque = l.Quantidade * p.PrecoVenda,
                p.PrecoVenda,
                VendidoPorPeso = p.ProdutoBalanca || p.VendidoFracionado
                                 || (u != null && (u.Pesavel || u.Sigla == "KG")),
                p.EtiquetaDesatualizada,
            }
        ).AsNoTracking().ToListAsync(ct);

        // Enriquece com status e alerta de promoção já gerada
        var alertasAtivos = await db.AlertasValidade.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.PromoGerada)
            .Select(a => a.LoteId)
            .ToListAsync(ct);

        var resultado = itens.Select(i =>
        {
            var dias   = i.DataValidade.HasValue ? (i.DataValidade.Value.Date - hoje).Days : 0;
            var status = ClassificarStatus(dias, cfg);
            return new
            {
                i.LoteId, i.ProdutoId, i.Descricao, i.DescricaoComplementar,
                i.Codigo, i.CodigoBarras, i.CodigoPlu, i.ImagemUrl,
                i.LoteImagemUrl,
                i.Marca, i.Categoria, i.NumeroLote,
                DataValidade    = i.DataValidade?.ToString("dd/MM/yyyy"),
                DataValidadeIso = i.DataValidade?.ToString("yyyy-MM-dd"),
                DiasRestantes   = dias,
                i.Quantidade,
                i.ValorEstoque,
                i.PrecoVenda,
                i.VendidoPorPeso,
                i.EtiquetaDesatualizada,
                Status          = status,   // Vencido | Urgente | Vermelho | Amarelo | Ok
                PromoGerada     = alertasAtivos.Contains(i.LoteId),
            };
        })
        .OrderBy(i => i.DiasRestantes)
        .ToList();

        var resumo = new
        {
            TotalProdutos = resultado.Count,
            Vencidos   = resultado.Count(i => i.Status == "Vencido"),
            Urgentes   = resultado.Count(i => i.Status == "Urgente"),
            Vermelhos  = resultado.Count(i => i.Status == "Vermelho"),
            Amarelos   = resultado.Count(i => i.Status == "Amarelo"),
            ValorEmRisco = resultado
                .Where(i => i.Status is "Urgente" or "Vermelho")
                .Sum(i => i.ValorEstoque),
            Configuracao = new
            {
                cfg.DiasAlertaAmarelo, cfg.DiasAlertaVermelho, cfg.DiasAlertaUrgente,
                cfg.DescontoAutoPercent, cfg.PromoAutomatica,
            }
        };

        return Ok(new { resumo, itens = resultado });
    }

    // ─── Identificar produto por código de barras ─────────────────────────

    [HttpGet("produto")]
    public async Task<IActionResult> ObterPorBarcode(
        [FromQuery] Guid empresaId, [FromQuery] string barcode, CancellationToken ct)
    {
        var produto = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo
                     && (p.CodigoBarras == barcode || p.Codigo == barcode))
            .Select(p => new
            {
                p.Id, p.Descricao, p.CodigoBarras, p.Codigo, p.ImagemUrl,
                MarcaId = p.MarcaId,
                CategoriaId = p.CategoriaId,
                p.PrecoVenda,
                p.ControlarValidade,
            })
            .FirstOrDefaultAsync(ct);

        if (produto is null)
            return NotFound(new { mensagem = $"Produto não encontrado para o código '{barcode}'." });

        // Busca marca e lotes existentes
        var marca = await db.Marcas.AsNoTracking()
            .Where(m => m.Id == produto.MarcaId)
            .Select(m => m.Nome)
            .FirstOrDefaultAsync(ct);

        var lotes = await db.Lotes.AsNoTracking()
            .Where(l => l.ProdutoId == produto.Id && l.Quantidade > 0)
            .OrderByDescending(l => l.DataValidade)
            .Select(l => new { l.Id, l.NumeroLote, l.DataValidade, l.Quantidade })
            .ToListAsync(ct);

        return Ok(new { produto, marca, lotes });
    }

    // ─── Produtos de uma Nota Fiscal de entrada (por número) ───────────────

    /// <summary>
    /// Lista os produtos de uma entrada de NF-e pelo número da nota, para
    /// registrar a validade item a item. O número da NF fica embutido na chave
    /// de acesso (posições 26–34, 9 dígitos), então filtramos por esse trecho.
    /// </summary>
    [HttpGet("por-nota")]
    public async Task<IActionResult> ProdutosPorNota(
        [FromQuery] Guid empresaId, [FromQuery] long numeroNota, CancellationToken ct)
    {
        if (numeroNota <= 0)
            return BadRequest(new { mensagem = "Informe o número da nota." });

        var numero9 = numeroNota.ToString("D9");

        var entradas = await db.EntradasNFe.AsNoTracking()
            .Include(e => e.Itens)
            .Where(e => e.EmpresaId == empresaId
                     && e.ChaveAcesso.Length == 44
                     && e.ChaveAcesso.Substring(25, 9) == numero9)
            .OrderByDescending(e => e.DataEntrada)
            .ToListAsync(ct);

        if (entradas.Count == 0)
            return NotFound(new { mensagem = $"Nenhuma entrada encontrada para a NF nº {numeroNota}." });

        // Só itens que viraram Produto (mercadoria) têm controle de validade.
        var produtoIds = entradas.SelectMany(e => e.Itens)
            .Where(i => i.ProdutoId.HasValue)
            .Select(i => i.ProdutoId!.Value)
            .Distinct()
            .ToList();

        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => produtoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Codigo, p.CodigoBarras, p.ImagemUrl, p.ControlarValidade })
            .ToDictionaryAsync(p => p.Id, ct);

        // Lotes com validade já existentes desses produtos. Assim reconhecemos como
        // "já feito" também o que foi registrado ANTES (gravava só o Lote, não o item).
        var lotes = await db.Lotes.AsNoTracking()
            .Where(l => produtoIds.Contains(l.ProdutoId) && l.DataValidade != null)
            .Select(l => new { l.Id, l.ProdutoId, l.DataValidade, l.ImagemUrl, l.CriadoEm })
            .ToListAsync(ct);
        var loteById = lotes.ToDictionary(l => l.Id);

        var itens = entradas.SelectMany(e => e.Itens
                .Where(i => i.ProdutoId.HasValue)
                .Select(i =>
                {
                    var pid = i.ProdutoId!.Value;
                    // "Já registrado" é específico DESTE item: a validade do próprio item OU o lote
                    // vinculado a ele. NÃO usa qualquer lote do produto (senão uma nota nova de um
                    // produto que já tem lote antigo viria toda "registrada" e travava o registro).
                    var lote = (i.LoteId.HasValue && loteById.TryGetValue(i.LoteId.Value, out var lb)) ? lb : null;
                    var validadeIso = i.Validade?.ToString("yyyy-MM-dd")
                                    ?? lote?.DataValidade?.ToString("yyyy-MM-dd");
                    return new
                    {
                        e.Id,                       // entradaId
                        EmitenteNome = e.EmitenteNome,
                        DataEmissao  = e.DataEmissao.ToString("dd/MM/yyyy"),
                        ItemId       = i.Id,
                        ProdutoId    = pid,
                        Descricao    = i.ProdutoDescricao ?? i.DescricaoXml,
                        CodigoBarras = produtos.TryGetValue(pid, out var p) ? p.CodigoBarras : i.CodigoBarras,
                        Codigo       = produtos.TryGetValue(pid, out var p2) ? p2.Codigo : null,
                        ImagemUrl    = produtos.TryGetValue(pid, out var p3) ? p3.ImagemUrl : null,
                        i.QuantidadeEstoque,
                        i.NumeroLote,
                        i.LoteId,
                        ValidadeIso   = validadeIso,
                        LoteImagemUrl = lote?.ImagemUrl,
                        // Registrado de fato = tem LOTE vinculado a este item. A validade sozinha
                        // (ex.: pré-preenchida do XML na importação) é só sugestão, ainda registrável.
                        JaRegistrado  = lote != null,
                    };
                }))
            .OrderBy(i => i.Descricao)
            .ToList();

        return Ok(new
        {
            numeroNota,
            emitente = entradas[0].EmitenteNome,
            totalItens = itens.Count,
            itens,
        });
    }

    // ─── Preencher validades a partir do XML já guardado da nota ────────────

    /// <summary>
    /// Relê o XML da NF (NotaFiscalRecebida.XmlNota) e devolve lote+validade por item,
    /// lendo o &lt;rastro&gt; (padrão oficial nLote/dVal) ou, na falta, o texto livre do
    /// infAdProd ("Lote: X Qtde: N Validade: DD/MM/YYYY"). Casa com os itens da entrada
    /// pelo código do fornecedor (cProd). Mais confiável que ler o PDF.
    /// </summary>
    [HttpGet("preencher-validades-nota")]
    public async Task<IActionResult> PreencherValidadesDaNota(
        [FromQuery] Guid empresaId, [FromQuery] long numeroNota, CancellationToken ct)
    {
        if (numeroNota <= 0) return BadRequest(new { mensagem = "Informe o número da nota." });
        var numero9 = numeroNota.ToString("D9");

        var entradas = await db.EntradasNFe.AsNoTracking()
            .Include(e => e.Itens)
            .Where(e => e.EmpresaId == empresaId
                     && e.ChaveAcesso.Length == 44
                     && e.ChaveAcesso.Substring(25, 9) == numero9)
            .ToListAsync(ct);
        if (entradas.Count == 0)
            return NotFound(new { mensagem = $"Nenhuma entrada encontrada para a NF nº {numeroNota}." });

        var chaves = entradas.Select(e => e.ChaveAcesso).Distinct().ToList();
        var xmls = await db.NotasFiscaisRecebidas.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId && chaves.Contains(n.ChaveAcesso) && n.XmlNota != null)
            .Select(n => new { n.ChaveAcesso, n.XmlNota })
            .ToListAsync(ct);
        var xmlPorChave = xmls.ToDictionary(x => x.ChaveAcesso, x => x.XmlNota!);

        string Norm(string? s) => (s ?? "").TrimStart('0').Trim();
        var resultado = new List<object>();
        var semXml = 0;

        foreach (var e in entradas)
        {
            if (!xmlPorChave.TryGetValue(e.ChaveAcesso, out var xml) || string.IsNullOrWhiteSpace(xml))
            {
                semXml++;
                continue;
            }

            System.Xml.Linq.XDocument doc;
            try { doc = System.Xml.Linq.XDocument.Parse(xml); }
            catch { semXml++; continue; }

            var ns = doc.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;
            // caso o XML seja o envelope nfeProc, procura os <det> em qualquer nível
            var dets = doc.Descendants(ns + "det").ToList();
            foreach (var det in dets)
            {
                var prod = det.Element(ns + "prod");
                if (prod is null) continue;
                var cProd = prod.Element(ns + "cProd")?.Value;

                var rastro = prod.Element(ns + "rastro");
                string? nLote = rastro?.Element(ns + "nLote")?.Value;
                DateTime? dVal = DateTime.TryParse(rastro?.Element(ns + "dVal")?.Value, out var dvR) ? dvR : null;
                if (nLote is null || dVal is null)
                {
                    var infAd = det.Element(ns + "infAdProd")?.Value;
                    if (!string.IsNullOrWhiteSpace(infAd))
                    {
                        if (nLote is null)
                        {
                            var mL = System.Text.RegularExpressions.Regex.Match(infAd,
                                @"Lote:\s*(.+?)\s+(?:Qtde|Qtd|Validade|Val)\b",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (mL.Success) nLote = mL.Groups[1].Value.Trim();
                        }
                        if (dVal is null)
                        {
                            var mV = System.Text.RegularExpressions.Regex.Match(infAd,
                                @"Validade:\s*(\d{2}/\d{2}/\d{4})",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (mV.Success && DateTime.TryParseExact(mV.Groups[1].Value, "dd/MM/yyyy",
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.None, out var dvV))
                                dVal = dvV;
                        }
                    }
                }
                if (nLote is null && dVal is null) continue;

                var item = e.Itens.FirstOrDefault(i =>
                    i.ProdutoId.HasValue && Norm(i.CodigoFornecedor) == Norm(cProd));
                if (item is null) continue;

                resultado.Add(new
                {
                    itemId = item.Id,
                    descricao = item.ProdutoDescricao ?? item.DescricaoXml,
                    lote = nLote,
                    validadeIso = dVal?.ToString("yyyy-MM-dd"),
                });
            }
        }

        if (resultado.Count == 0)
            return Ok(new
            {
                casados = 0,
                itens = new object[0],
                aviso = semXml > 0
                    ? "O XML desta nota não está guardado (ou não traz lote/validade). Use o PDF."
                    : "O XML desta nota não traz lote/validade nos itens.",
            });

        return Ok(new { casados = resultado.Count, itens = resultado });
    }

    // ─── Registrar validade ────────────────────────────────────────────────

    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar([FromBody] RegistrarValidadeRequest req, CancellationToken ct)
    {
        Domain.Estoque.Entities.Lote lote;
        string mensagem;

        if (req.LoteId.HasValue)
        {
            lote = await db.Lotes.FindAsync([req.LoteId.Value], ct)
                ?? throw new KeyNotFoundException("Lote não encontrado.");
            lote.AtualizarValidade(req.DataValidade);
            mensagem = "Validade atualizada.";
        }
        else
        {
            var produto = await db.Produtos.FindAsync([req.ProdutoId], ct);
            if (produto is null) return NotFound("Produto não encontrado.");

            // Usa a loja informada (ativa/do atendente); só cai no 1º local se não vier nada.
            var localId = req.LocalEstoqueId
                ?? await db.LocaisEstoque.AsNoTracking()
                    .Where(l => l.EmpresaId == req.EmpresaId)
                    .Select(l => l.Id)
                    .FirstOrDefaultAsync(ct);

            var numero = req.NumeroLote ?? $"L{DateTime.Today:yyyyMMdd}";

            // Se já existe um lote com essa chave (ex.: criado na escrituração da NF),
            // atualiza em vez de duplicar (evita violação do índice único).
            var existente = await db.Lotes.FirstOrDefaultAsync(l =>
                l.ProdutoId == req.ProdutoId && l.LocalEstoqueId == localId && l.NumeroLote == numero, ct);
            if (existente is not null)
            {
                existente.AtualizarValidade(req.DataValidade);
                lote = existente;
                mensagem = "Validade atualizada no lote existente.";
            }
            else
            {
                lote = Lote.Criar(req.EmpresaId, req.ProdutoId, localId, numero,
                    req.Quantidade ?? 1, produto.CustoUnitario, dataValidade: req.DataValidade);
                db.Lotes.Add(lote);
                mensagem = "Lote registrado com validade.";
            }
        }

        // Salva a foto da etiqueta (conferência) e vincula ao lote.
        var url = await SalvarImagemLoteAsync(lote.Id, req.ImagemBase64, ct);
        if (url is not null) lote.DefinirImagem(url);

        // Mudou a validade → a etiqueta do produto precisa ser reimpressa.
        var prodEtiq = await db.Produtos.FindAsync([lote.ProdutoId], ct);
        prodEtiq?.MarcarEtiquetaDesatualizada();

        // Marca o item da NOTA como concluído (validade + lote), para o progresso
        // "já registrei este" persistir mesmo se recarregar a tela / sair pro almoço.
        if (req.ItemEntradaId.HasValue)
        {
            var item = await db.ItensEntradaNFe.FirstOrDefaultAsync(i => i.Id == req.ItemEntradaId.Value, ct);
            item?.DefinirLote(lote.NumeroLote, req.DataValidade, lote.Id);
        }

        await uow.SalvarAsync(ct);
        return Ok(new { lote.Id, mensagem, imagemUrl = lote.ImagemUrl });
    }

    /// <summary>
    /// Decodifica a foto (dataURL/base64), redimensiona (máx. 1024px, JPEG q60) para
    /// não ocupar muito espaço no servidor mantendo a legibilidade, salva em
    /// wwwroot/uploads/lotes e retorna a URL.
    /// </summary>
    private static async Task<string?> SalvarImagemLoteAsync(Guid loteId, string? base64, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            var dados = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
            var bytes = Convert.FromBase64String(dados);

            var dir = Path.Combine("wwwroot", "uploads", "lotes");
            Directory.CreateDirectory(dir);
            var nome = $"{loteId}.jpg";

            using var image = SixLabors.ImageSharp.Image.Load(bytes);
            image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
            {
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max,   // mantém proporção
                Size = new SixLabors.ImageSharp.Size(1024, 1024),        // maior lado ≤ 1024px
            }));
            await image.SaveAsJpegAsync(
                Path.Combine(dir, nome),
                new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 60 }, ct);

            return $"/uploads/lotes/{nome}";
        }
        catch { return null; }
    }

    // ─── Executar o monitoramento agora (gera promoções) ───────────────────

    /// <summary>Roda o monitoramento de validade na hora (mesma lógica das 8h):
    /// classifica lotes e gera promoções automáticas para os que estão no vermelho.</summary>
    [HttpPost("gerar-promocoes")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> GerarPromocoesAgora(CancellationToken ct)
    {
        var job = ActivatorUtilities.CreateInstance<Sistema.Infrastructure.Jobs.ValidadeJob>(
            HttpContext.RequestServices);
        await job.ExecutarAsync();
        return Ok(new { mensagem = "Monitoramento executado. Promoções atualizadas." });
    }

    // ─── Configurações ─────────────────────────────────────────────────────

    [HttpGet("configuracoes")]
    public async Task<IActionResult> ObterConfig([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var cfg = await CarregarConfig(empresaId, ct);
        return Ok(new
        {
            cfg.Id, cfg.DiasAlertaAmarelo, cfg.DiasAlertaVermelho, cfg.DiasAlertaUrgente,
            cfg.PromoAutomatica, cfg.ExigeAprovacao, cfg.DescontoAutoPercent,
            cfg.BloqueioVendaVencido, cfg.CategoriasJson,
        });
    }

    [HttpPut("configuracoes")]
    public async Task<IActionResult> SalvarConfig(
        [FromQuery] Guid empresaId, [FromBody] SalvarConfigRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesValidade
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        if (cfg is null)
        {
            cfg = ConfiguracaoValidade.Padrao(empresaId);
            db.ConfiguracoesValidade.Add(cfg);
        }

        cfg.Atualizar(req.DiasAlertaAmarelo, req.DiasAlertaVermelho, req.DiasAlertaUrgente,
            req.PromoAutomatica, req.ExigeAprovacao, req.DescontoAutoPercent,
            req.BloqueioVendaVencido, req.CategoriasJson);

        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<ConfiguracaoValidade> CarregarConfig(Guid empresaId, CancellationToken ct)
        => await db.ConfiguracoesValidade.AsNoTracking()
               .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct)
           ?? ConfiguracaoValidade.Padrao(empresaId);

    private static string ClassificarStatus(int dias, ConfiguracaoValidade cfg)
    {
        if (dias < 0)                        return "Vencido";
        if (dias <= cfg.DiasAlertaUrgente)   return "Urgente";
        if (dias <= cfg.DiasAlertaVermelho)  return "Vermelho";
        if (dias <= cfg.DiasAlertaAmarelo)   return "Amarelo";
        return "Ok";
    }
}

public record RegistrarValidadeRequest(
    Guid EmpresaId, Guid ProdutoId, DateTime DataValidade,
    Guid? LoteId = null, string? NumeroLote = null, decimal? Quantidade = null,
    string? ImagemBase64 = null, Guid? ItemEntradaId = null, Guid? LocalEstoqueId = null);

public partial class ValidadeController
{
    /// <summary>Lê o PDF (DANFE) da nota e extrai Lote/Validade de cada item (padrão
    /// "Lote: X Qtde: N Validade: DD/MM/YYYY"), casando com os itens da entrada pelo código
    /// do fornecedor. Retorna o que preencher — NÃO grava (a tela confere e registra).</summary>
    [HttpPost("importar-validades-pdf")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> ImportarValidadesPdf([FromForm] Guid empresaId, IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0) return BadRequest(new { mensagem = "Envie o PDF da nota." });

        var sb = new System.Text.StringBuilder();
        using (var stream = arquivo.OpenReadStream())
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(stream))
            foreach (var page in pdf.GetPages())
            {
                var linhas = page.GetWords()
                    .GroupBy(w => System.Math.Round(w.BoundingBox.Bottom, 0))
                    .OrderByDescending(g => g.Key)
                    .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
                foreach (var l in linhas) sb.AppendLine(l);
            }
        var flat = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ");

        // "Lote: <lote> Qtde: N Validade: DD/MM/YYYY ... <cod> <NCM8> <CST> <CFOP4> <UNID>"
        var rx = new System.Text.RegularExpressions.Regex(
            @"Lote:\s*(?<lote>.+?)\s+Qtde:\s*\d+\s+Validade:\s*(?<val>\d{2}/\d{2}/\d{4}).*?(?<cod>\d{3,7})\s+\d{8}\s+\d{2,3}\s+\d{4}\s+[A-Z]{1,3}\b",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        var lidos = rx.Matches(flat).Select(m => new
        {
            cod = m.Groups["cod"].Value,
            lote = m.Groups["lote"].Value.Trim(),
            val = System.DateTime.TryParseExact(m.Groups["val"].Value, "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : (System.DateTime?)null
        }).Where(x => x.val.HasValue).ToList();

        if (lidos.Count == 0)
            return Ok(new { casados = 0, total = 0, itens = new object[0], aviso = "Não consegui ler validades neste PDF." });

        // Entrada pela chave (44 dígitos) no PDF
        var chave = System.Text.RegularExpressions.Regex.Match(flat.Replace(" ", ""), @"\d{44}").Value;
        var itensEntrada = await (from i in db.ItensEntradaNFe.AsNoTracking()
                join e in db.EntradasNFe.AsNoTracking() on i.EntradaNFeId equals e.Id
                where e.EmpresaId == empresaId && (chave == "" || e.ChaveAcesso == chave)
                select new { i.Id, i.CodigoFornecedor, i.ProdutoDescricao, i.ProdutoId })
            .ToListAsync(ct);

        string Norm(string? s) => (s ?? "").TrimStart('0').Trim();
        var resultado = new List<object>();
        foreach (var it in itensEntrada)
        {
            var achou = lidos.FirstOrDefault(x => Norm(x.cod) == Norm(it.CodigoFornecedor));
            if (achou is null) continue;
            resultado.Add(new
            {
                itemId = it.Id,
                descricao = it.ProdutoDescricao,
                lote = achou.lote,
                validadeIso = achou.val!.Value.ToString("yyyy-MM-dd")
            });
        }

        return Ok(new { casados = resultado.Count, total = lidos.Count, itens = resultado });
    }
}

public record SalvarConfigRequest(
    int DiasAlertaAmarelo, int DiasAlertaVermelho, int DiasAlertaUrgente,
    bool PromoAutomatica, bool ExigeAprovacao, decimal DescontoAutoPercent,
    bool BloqueioVendaVencido, string? CategoriasJson = null);
