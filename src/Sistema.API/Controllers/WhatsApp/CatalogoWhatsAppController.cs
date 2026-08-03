using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.WhatsApp;

[ApiController]
[Route("api/whatsapp/catalogo")]
[Authorize]
public class CatalogoWhatsAppController(SistemaDbContext db, IUnitOfWork uow,
    Sistema.Infrastructure.Services.WhatsAppCloudApiService whatsApp) : ControllerBase
{
    /// <summary>Sincroniza os produtos (ativos, com preço e foto) com o catálogo comercial da Meta.</summary>
    [HttpPost("sincronizar")]
    public async Task<IActionResult> Sincronizar([FromBody] SincronizarCatalogoRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId && c.LocalEstoqueId == req.LocalEstoqueId, ct);
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.CatalogId) || string.IsNullOrWhiteSpace(cfg.AccessToken))
            return BadRequest(new { mensagem = "Configure o Catalog ID e o Access Token antes de sincronizar." });

        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == req.EmpresaId && p.Ativo && p.PrecoVenda > 0)
            .ToListAsync(ct);
        var comFoto = produtos.Where(p => !string.IsNullOrEmpty(p.ImagemUrl)).ToList();
        var semFoto = produtos.Count - comFoto.Count;

        if (comFoto.Count == 0)
            return Ok(new { ok = false, enviados = 0, semFoto,
                mensagem = "Nenhum produto com foto. O catálogo da Meta EXIGE imagem — cadastre fotos primeiro (botão 'Buscar foto (EAN)' em Produtos)." });

        const string baseImg = "https://sistema.ecogranel.com.br";
        var lista = comFoto.Select(p => new Sistema.Infrastructure.Services.WhatsAppCloudApiService.CatalogoProdutoMeta(
            RetailerId: p.Codigo,
            Name: p.Descricao.Length > 150 ? p.Descricao[..150] : p.Descricao,
            Description: p.DescricaoComplementar,
            PriceCents: (int)Math.Round(p.PrecoVenda * 100),
            ImageUrl: baseImg + p.ImagemUrl,
            Url: $"https://ecogranel.com.br/produtos/produto.php?p={Sistema.Infrastructure.Services.SiteSyncService.Slugify(p.Descricao)}",
            Disponivel: true)).ToList();

        var (ok, enviados, msg) = await whatsApp.SincronizarCatalogoAsync(cfg.CatalogId, cfg.AccessToken, lista, ct);
        return ok
            ? Ok(new { ok, enviados, semFoto,
                mensagem = $"{enviados} produto(s) enviado(s) ao catálogo." + (semFoto > 0 ? $" {semFoto} sem foto foram ignorados." : "") })
            : StatusCode(502, new { ok, mensagem = msg });
    }

    /// <summary>Lista catálogos da empresa.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.CatalogosWhatsApp.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .ToListAsync(ct));

    /// <summary>Cria um novo catálogo WhatsApp.</summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarCatalogoRequest req, CancellationToken ct)
    {
        var provedor = Enum.Parse<ProvedorWhatsApp>(req.Provedor);
        var catalogo = CatalogoWhatsApp.Criar(req.EmpresaId, req.Nome, provedor, req.Descricao);
        db.CatalogosWhatsApp.Add(catalogo);
        await uow.SalvarAsync(ct);
        return Ok(new { catalogo.Id });
    }

    /// <summary>Obtém catálogo com seus itens.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var catalogo = await db.CatalogosWhatsApp
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return catalogo is null ? NotFound() : Ok(catalogo);
    }

    /// <summary>Adiciona produto ao catálogo.</summary>
    [HttpPost("{id:guid}/itens")]
    public async Task<IActionResult> AdicionarItem(Guid id, [FromBody] AdicionarItemCatalogoRequest req, CancellationToken ct)
    {
        var catalogo = await db.CatalogosWhatsApp
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Catálogo não encontrado.");

        catalogo.AdicionarItem(req.ProdutoId, req.Descricao, req.Preco, req.UrlFoto, req.Disponivel);
        db.CatalogosWhatsApp.Update(catalogo);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Remove produto do catálogo.</summary>
    [HttpDelete("{id:guid}/itens/{produtoId:guid}")]
    public async Task<IActionResult> RemoverItem(Guid id, Guid produtoId, CancellationToken ct)
    {
        var catalogo = await db.CatalogosWhatsApp
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Catálogo não encontrado.");

        catalogo.RemoverItem(produtoId);
        db.CatalogosWhatsApp.Update(catalogo);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Lista pedidos recebidos via WhatsApp.</summary>
    [HttpGet("pedidos")]
    public async Task<IActionResult> Pedidos([FromQuery] Guid empresaId,
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.PedidosWhatsApp.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusPedidoWhatsApp>(status, out var st))
            query = query.Where(p => p.Status == st);

        return Ok(await query.OrderByDescending(p => p.CriadoEm).ToListAsync(ct));
    }
}

public record CriarCatalogoRequest(Guid EmpresaId, string Nome, string Provedor, string? Descricao = null);
public record SincronizarCatalogoRequest(Guid EmpresaId, Guid? LocalEstoqueId = null);
public record AdicionarItemCatalogoRequest(Guid ProdutoId, string Descricao, decimal Preco, string? UrlFoto = null, bool Disponivel = true);
