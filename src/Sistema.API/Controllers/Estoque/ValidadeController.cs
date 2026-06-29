using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

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
                p.CodigoBarras,
                p.ImagemUrl,
                Marca        = m != null ? m.Nome : "",
                Categoria    = c != null ? c.Nome : "",
                l.NumeroLote,
                l.DataValidade,
                l.Quantidade,
                ValorEstoque = l.Quantidade * p.PrecoVenda,
                p.PrecoVenda,
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
                i.LoteId, i.ProdutoId, i.Descricao, i.CodigoBarras, i.ImagemUrl,
                i.Marca, i.Categoria, i.NumeroLote,
                DataValidade    = i.DataValidade?.ToString("dd/MM/yyyy"),
                DiasRestantes   = dias,
                i.Quantidade,
                i.ValorEstoque,
                i.PrecoVenda,
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

    // ─── Registrar validade ────────────────────────────────────────────────

    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar([FromBody] RegistrarValidadeRequest req, CancellationToken ct)
    {
        if (req.LoteId.HasValue)
        {
            // Atualiza lote existente
            var lote = await db.Lotes.FindAsync([req.LoteId.Value], ct);
            if (lote is null) return NotFound("Lote não encontrado.");
            lote.AtualizarValidade(req.DataValidade);
            await uow.SalvarAsync(ct);
            return Ok(new { lote.Id, mensagem = "Validade atualizada." });
        }
        else
        {
            // Cria novo lote
            var produto = await db.Produtos.FindAsync([req.ProdutoId], ct);
            if (produto is null) return NotFound("Produto não encontrado.");

            var localId = await db.LocaisEstoque.AsNoTracking()
                .Where(l => l.EmpresaId == req.EmpresaId)
                .Select(l => l.Id)
                .FirstOrDefaultAsync(ct);

            var numero = req.NumeroLote ?? $"L{DateTime.Today:yyyyMMdd}";
            var lote   = Lote.Criar(req.EmpresaId, req.ProdutoId, localId, numero,
                req.Quantidade ?? 1, produto.CustoUnitario,
                dataValidade: req.DataValidade);

            db.Lotes.Add(lote);
            await uow.SalvarAsync(ct);
            return Ok(new { lote.Id, mensagem = "Lote registrado com validade." });
        }
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
    Guid? LoteId = null, string? NumeroLote = null, decimal? Quantidade = null);

public record SalvarConfigRequest(
    int DiasAlertaAmarelo, int DiasAlertaVermelho, int DiasAlertaUrgente,
    bool PromoAutomatica, bool ExigeAprovacao, decimal DescontoAutoPercent,
    bool BloqueioVendaVencido, string? CategoriasJson = null);
