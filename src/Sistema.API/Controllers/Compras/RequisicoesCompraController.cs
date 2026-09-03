using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Compras;

/// <summary>
/// Requisições de compra: o atendente pede (produto + quantidade, sem fornecedor/preço);
/// o gestor vê agrupado por fornecedor e gera os pedidos de compra.
/// </summary>
[ApiController]
[Route("api/requisicoes-compra")]
[Authorize]
public class RequisicoesCompraController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    /// <summary>Cria uma requisição (atendente). Só produto + quantidade.</summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarRequisicaoRequest req, CancellationToken ct)
    {
        if (req.Itens is null || req.Itens.Count == 0)
            return BadRequest(new { mensagem = "Adicione ao menos um produto à requisição." });

        var requisicao = RequisicaoCompra.Criar(req.EmpresaId, req.UsuarioId, req.LocalEstoqueId, req.Observacao);

        var ids = req.Itens.Select(i => i.ProdutoId).ToList();
        var descricoes = await db.Produtos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Descricao })
            .ToDictionaryAsync(p => p.Id, p => p.Descricao, ct);

        foreach (var it in req.Itens)
        {
            if (it.Quantidade <= 0) continue;
            var desc = descricoes.TryGetValue(it.ProdutoId, out var d) ? d : "(produto)";
            requisicao.AdicionarItem(it.ProdutoId, desc, it.Quantidade);
        }

        db.RequisicoesCompra.Add(requisicao);
        await uow.SalvarAsync(ct);
        return Ok(new { requisicao.Id });
    }

    /// <summary>Lista requisições. Atendente vê as da própria loja; gestor vê todas.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, [FromQuery] string? status,
        CancellationToken ct)
    {
        var q = db.RequisicoesCompra.AsNoTracking().Where(r => r.EmpresaId == empresaId);

        if (User.IsInRole("Atendente"))
        {
            var loja = Guid.TryParse(User.FindFirst("localEstoqueId")?.Value, out var lid) ? lid : Guid.Empty;
            q = q.Where(r => r.LocalEstoqueId == loja);
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<StatusRequisicaoCompra>(status, out var st))
            q = q.Where(r => r.Status == st);

        var lista = await q.OrderByDescending(r => r.CriadoEm)
            .Select(r => new
            {
                r.Id, r.CriadoEm, r.LocalEstoqueId, r.UsuarioId,
                status = r.Status.ToString(),
                qtdItens = r.Itens.Count,
            })
            .ToListAsync(ct);

        var usuarioIds = lista.Select(x => x.UsuarioId).Distinct().ToList();
        var lojaIds = lista.Where(x => x.LocalEstoqueId.HasValue).Select(x => x.LocalEstoqueId!.Value).Distinct().ToList();
        var usuarios = await db.Usuarios.AsNoTracking().Where(u => usuarioIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome }).ToDictionaryAsync(u => u.Id, u => u.Nome, ct);
        var lojas = await db.LocaisEstoque.AsNoTracking().Where(l => lojaIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Nome }).ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        return Ok(lista.Select(x => new
        {
            x.Id, x.CriadoEm, x.status, x.qtdItens,
            solicitante = usuarios.GetValueOrDefault(x.UsuarioId, "—"),
            loja = x.LocalEstoqueId.HasValue ? lojas.GetValueOrDefault(x.LocalEstoqueId.Value, "—") : "—",
        }));
    }

    /// <summary>Detalhe: itens já com fornecedor principal e custo, para agrupar no cliente.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var req = await db.RequisicoesCompra.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return NotFound();

        var itens = await db.ItensRequisicaoCompra.AsNoTracking()
            .Where(i => i.RequisicaoCompraId == id).ToListAsync(ct);

        var prodIds = itens.Select(i => i.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Descricao, p.CustoUnitario, p.FornecedorPrincipalId })
            .ToListAsync(ct);
        var pmap = produtos.ToDictionary(p => p.Id);

        var fornIds = produtos.Where(p => p.FornecedorPrincipalId.HasValue)
            .Select(p => p.FornecedorPrincipalId!.Value).Distinct().ToList();
        var forns = await db.Fornecedores.AsNoTracking().Where(f => fornIds.Contains(f.Id))
            .Select(f => new { f.Id, f.RazaoSocial }).ToDictionaryAsync(f => f.Id, f => f.RazaoSocial, ct);

        return Ok(new
        {
            req.Id, status = req.Status.ToString(), req.Observacao, req.CriadoEm,
            itens = itens.Select(i =>
            {
                var p = pmap.GetValueOrDefault(i.ProdutoId);
                return new
                {
                    produtoId = i.ProdutoId,
                    descricao = i.Descricao,
                    quantidade = i.Quantidade,
                    custoUnitario = p?.CustoUnitario ?? 0m,
                    fornecedorId = p?.FornecedorPrincipalId,
                    fornecedor = p?.FornecedorPrincipalId is Guid fid && forns.TryGetValue(fid, out var fn)
                        ? fn : "(sem fornecedor)",
                };
            }).ToList()
        });
    }

    [HttpPatch("{id:guid}/processar")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<IActionResult> Processar(Guid id, CancellationToken ct)
    {
        var req = await db.RequisicoesCompra.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Requisição não encontrada.");
        req.Processar();
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
    {
        var req = await db.RequisicoesCompra.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Requisição não encontrada.");
        req.Cancelar();
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarRequisicaoRequest(
    Guid EmpresaId, Guid UsuarioId, Guid? LocalEstoqueId, string? Observacao,
    List<ItemRequisicaoRequest> Itens);

public record ItemRequisicaoRequest(Guid ProdutoId, decimal Quantidade);
