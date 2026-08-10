using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Publico;

/// <summary>
/// VITRINE pública (e-commerce): navegação de produtos sem login e envio de pedido.
/// O pedido vira um PedidoWhatsApp na loja escolhida — a loja recebe na tela de Pedidos.
/// Preços são SEMPRE recalculados no servidor (nunca confia no valor vindo do cliente).
/// </summary>
[ApiController]
[Route("api/publico/vitrine")]
[AllowAnonymous]
public class VitrineController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    /// <summary>Config da loja para montar a vitrine: nome, lojas (com WhatsApp) e categorias.</summary>
    [HttpGet("{empresaId:guid}")]
    public async Task<IActionResult> Config(Guid empresaId, CancellationToken ct)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => new { nome = e.NomeFantasia ?? e.RazaoSocial })
            .FirstOrDefaultAsync(ct);
        if (empresa is null) return NotFound(new { mensagem = "Loja não encontrada." });

        var lojas = await (from l in db.LocaisEstoque.AsNoTracking()
                           where l.EmpresaId == empresaId && l.Ativo
                           join c in db.ConfiguracoesWhatsAppMensagem.AsNoTracking()
                               on l.Id equals c.LocalEstoqueId into cj
                           from c in cj.DefaultIfEmpty()
                           orderby l.Principal descending, l.Nome
                           select new { id = l.Id, nome = l.Nome, whatsapp = c.NumeroWhatsApp })
                          .ToListAsync(ct);

        // Categorias que têm ao menos 1 produto ativo com preço.
        var categorias = await (from p in db.Produtos.AsNoTracking()
                                where p.EmpresaId == empresaId && p.Ativo && p.PrecoVenda > 0
                                join c in db.Categorias.AsNoTracking() on p.CategoriaId equals c.Id
                                select c.Nome).Distinct().OrderBy(n => n).ToListAsync(ct);

        return Ok(new { empresa = empresa.nome, lojas, categorias });
    }

    /// <summary>Lista os produtos da vitrine (ativos, com preço). Imagem opcional.</summary>
    [HttpGet("{empresaId:guid}/produtos")]
    public async Task<IActionResult> Produtos(Guid empresaId, CancellationToken ct)
    {
        var itens = await (from p in db.Produtos.AsNoTracking()
                           where p.EmpresaId == empresaId && p.Ativo && p.PrecoVenda > 0
                           join c in db.Categorias.AsNoTracking() on p.CategoriaId equals c.Id into cj
                           from c in cj.DefaultIfEmpty()
                           orderby p.Descricao
                           select new
                           {
                               id = p.Id,
                               descricao = p.Descricao,
                               precoVenda = p.PrecoVenda,
                               imagemUrl = p.ImagemUrl,
                               categoria = c.Nome,
                               porPeso = p.ProdutoBalanca || p.VendidoFracionado,
                           }).ToListAsync(ct);

        return Ok(itens);
    }

    /// <summary>Recebe o pedido feito na vitrine e cria um PedidoWhatsApp na loja escolhida.</summary>
    [HttpPost("{empresaId:guid}/pedido")]
    public async Task<IActionResult> CriarPedido(Guid empresaId,
        [FromBody] PedidoVitrineRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NomeCliente) || string.IsNullOrWhiteSpace(req.Telefone))
            return BadRequest(new { mensagem = "Informe seu nome e telefone." });
        if (req.Itens is null || req.Itens.Count == 0)
            return BadRequest(new { mensagem = "Seu carrinho está vazio." });

        var tipoEntrega = string.Equals(req.TipoEntrega, "Entrega", StringComparison.OrdinalIgnoreCase)
            ? TipoEntregaWhatsApp.Entrega : TipoEntregaWhatsApp.Retirada;
        if (tipoEntrega == TipoEntregaWhatsApp.Entrega && string.IsNullOrWhiteSpace(req.EnderecoEntrega))
            return BadRequest(new { mensagem = "Informe o endereço para entrega." });

        // Preços recalculados no servidor a partir dos produtos ativos da empresa.
        var ids = req.Itens.Select(i => i.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo && ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Descricao, p.PrecoVenda })
            .ToDictionaryAsync(p => p.Id, ct);

        // Valida a loja (se informada) — senão usa a loja principal.
        Guid? localEstoqueId = req.LocalEstoqueId;
        if (localEstoqueId is null)
            localEstoqueId = await db.LocaisEstoque.AsNoTracking()
                .Where(l => l.EmpresaId == empresaId && l.Ativo)
                .OrderByDescending(l => l.Principal)
                .Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);

        var numero = $"V{await db.PedidosWhatsApp.CountAsync(p => p.EmpresaId == empresaId, ct) + 1:D4}";
        var telefone = new string(req.Telefone.Where(char.IsDigit).ToArray());

        var pedido = PedidoWhatsApp.Criar(empresaId, telefone, req.NomeCliente.Trim(),
            numero, tipoEntrega, localEstoqueId: localEstoqueId);

        foreach (var item in req.Itens)
        {
            if (item.Quantidade <= 0) continue;
            if (!produtos.TryGetValue(item.ProdutoId, out var prod)) continue;
            pedido.AdicionarItem(prod.Id, prod.Descricao, item.Quantidade, prod.PrecoVenda);
        }

        if (pedido.Itens.Count == 0)
            return BadRequest(new { mensagem = "Nenhum produto válido no carrinho." });

        if (tipoEntrega == TipoEntregaWhatsApp.Entrega)
            pedido.DefinirEndereco(req.EnderecoEntrega!.Trim());
        pedido.DefinirObservacao(req.Observacao);

        db.PedidosWhatsApp.Add(pedido);
        await uow.SalvarAsync(ct);

        return Ok(new { pedido.Numero, pedido.Total, itens = pedido.Itens.Count });
    }
}

public record ItemPedidoVitrine(Guid ProdutoId, decimal Quantidade);
public record PedidoVitrineRequest(
    string NomeCliente, string Telefone, Guid? LocalEstoqueId,
    string? TipoEntrega, string? EnderecoEntrega, string? Observacao,
    List<ItemPedidoVitrine> Itens);
