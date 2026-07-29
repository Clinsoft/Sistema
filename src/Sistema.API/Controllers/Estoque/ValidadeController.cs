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
public class ValidadeController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
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

        var itens = entradas.SelectMany(e => e.Itens
                .Where(i => i.ProdutoId.HasValue)
                .Select(i => new
                {
                    e.Id,                       // entradaId
                    EmitenteNome = e.EmitenteNome,
                    DataEmissao  = e.DataEmissao.ToString("dd/MM/yyyy"),
                    ItemId       = i.Id,
                    ProdutoId    = i.ProdutoId!.Value,
                    Descricao    = i.ProdutoDescricao ?? i.DescricaoXml,
                    CodigoBarras = produtos.TryGetValue(i.ProdutoId!.Value, out var p) ? p.CodigoBarras : i.CodigoBarras,
                    Codigo       = produtos.TryGetValue(i.ProdutoId!.Value, out var p2) ? p2.Codigo : null,
                    ImagemUrl    = produtos.TryGetValue(i.ProdutoId!.Value, out var p3) ? p3.ImagemUrl : null,
                    i.QuantidadeEstoque,
                    i.NumeroLote,
                    i.LoteId,
                    ValidadeIso  = i.Validade?.ToString("yyyy-MM-dd"),
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

            var localId = await db.LocaisEstoque.AsNoTracking()
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
    string? ImagemBase64 = null);

public record SalvarConfigRequest(
    int DiasAlertaAmarelo, int DiasAlertaVermelho, int DiasAlertaUrgente,
    bool PromoAutomatica, bool ExigeAprovacao, decimal DescontoAutoPercent,
    bool BloqueioVendaVencido, string? CategoriasJson = null);
