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
public class CatalogoWhatsAppController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
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
public record AdicionarItemCatalogoRequest(Guid ProdutoId, string Descricao, decimal Preco, string? UrlFoto = null, bool Disponivel = true);
