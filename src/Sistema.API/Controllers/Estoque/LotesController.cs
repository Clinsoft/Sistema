using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.API.Extensions;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/lotes")]
[Authorize]
public class LotesController(ILoteRepository repo, SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet("produto/{produtoId:guid}")]
    public async Task<IActionResult> ListarPorProduto(Guid produtoId, [FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await repo.ListarPorProdutoAsync(empresaId, produtoId, ct));

    /// <summary>Lista todos os lotes da empresa, com filtro opcional por local e por texto do produto.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] Guid? localEstoqueId,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        localEstoqueId = User.EscoparLoja(localEstoqueId);   // atendente: sempre a própria loja
        var hoje = DateTime.Today;
        var query =
            from l in db.Lotes.AsNoTracking()
            join p in db.Produtos.AsNoTracking() on l.ProdutoId equals p.Id
            where l.EmpresaId == empresaId
            select new { l, p.Descricao, p.Codigo };

        if (localEstoqueId.HasValue)
            query = query.Where(x => x.l.LocalEstoqueId == localEstoqueId.Value);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Descricao.Contains(q) || x.Codigo.Contains(q)
                                  || x.l.NumeroLote.Contains(q));

        var lotes = await query
            .OrderBy(x => x.l.DataValidade == null)
            .ThenBy(x => x.l.DataValidade)
            .ToListAsync(ct);

        return Ok(lotes.Select(x => new
        {
            x.l.Id, x.l.ProdutoId, x.l.LocalEstoqueId,
            descricao = x.Descricao, codigo = x.Codigo,
            x.l.NumeroLote, x.l.Quantidade, x.l.CustoUnitario,
            x.l.DataValidade, x.l.DataFabricacao, x.l.ImagemUrl,
            Vencido = x.l.EstaVencido(),
            VenceEm30 = x.l.VenceEm(30),
        }));
    }

    [HttpGet("vencimentos")]
    public async Task<IActionResult> Vencimentos([FromQuery] Guid empresaId, [FromQuery] int diasAlerta = 30, CancellationToken ct = default)
    {
        var lotes = await repo.ListarVencidosOuProximosAsync(empresaId, diasAlerta, ct);
        var produtoIds = lotes.Select(l => l.ProdutoId).Distinct().ToList();
        var produtos = produtoIds.Count > 0
            ? await db.Produtos.AsNoTracking()
                .Where(p => produtoIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => new { p.Codigo, p.Descricao }, ct)
            : new();

        return Ok(lotes.Select(l => new
        {
            l.Id, l.ProdutoId, l.NumeroLote, l.Quantidade, l.CustoUnitario,
            l.DataValidade, l.DataFabricacao, l.LocalEstoqueId,
            descricao = produtos.TryGetValue(l.ProdutoId, out var p) ? p.Descricao : "Produto",
            codigo = produtos.TryGetValue(l.ProdutoId, out var pc) ? pc.Codigo : null,
            // aliases mantidos para compatibilidade
            produtoDescricao = produtos.TryGetValue(l.ProdutoId, out var p2) ? p2.Descricao : "Produto",
            produtoCodigo = produtos.TryGetValue(l.ProdutoId, out var pc2) ? pc2.Codigo : null,
            Vencido = l.EstaVencido(),
            VenceEm30 = l.VenceEm(30)
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarLoteRequest req, CancellationToken ct)
    {
        var lote = Lote.Criar(req.EmpresaId, req.ProdutoId, req.LocalEstoqueId,
            req.NumeroLote, req.Quantidade, req.CustoUnitario,
            req.DataFabricacao, req.DataValidade);
        await repo.AdicionarAsync(lote, ct);
        await uow.SalvarAsync(ct);
        return Ok(new { lote.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] EditarLoteRequest req, CancellationToken ct)
    {
        var lote = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lote não encontrado.");
        lote.Editar(req.NumeroLote, req.Quantidade, req.CustoUnitario, req.DataFabricacao, req.DataValidade);
        repo.Atualizar(lote);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var lote = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lote não encontrado.");
        repo.Remover(lote);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarLoteRequest(
    Guid EmpresaId, Guid ProdutoId, Guid LocalEstoqueId,
    string NumeroLote, decimal Quantidade, decimal CustoUnitario,
    DateTime? DataFabricacao = null, DateTime? DataValidade = null);

public record EditarLoteRequest(
    string NumeroLote, decimal Quantidade, decimal CustoUnitario,
    DateTime? DataFabricacao = null, DateTime? DataValidade = null);
