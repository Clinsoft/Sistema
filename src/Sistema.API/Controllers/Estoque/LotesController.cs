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
        [FromQuery] bool incluirZerados,
        CancellationToken ct)
    {
        localEstoqueId = User.EscoparLoja(localEstoqueId);   // atendente: sempre a própria loja
        var hoje = DateTime.Today;
        var query =
            from l in db.Lotes.AsNoTracking()
            join p in db.Produtos.AsNoTracking() on l.ProdutoId equals p.Id
            where l.EmpresaId == empresaId
            select new { l, p.Descricao, p.Codigo };

        // Lote já baixado/destinado (saldo 0) some da lista por padrão — só reaparece se pedido.
        if (!incluirZerados)
            query = query.Where(x => x.l.Quantidade > 0);

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

    /// <summary>Dá destino ao lote (vencido): baixa a quantidade do estoque e registra o
    /// motivo. "Descarte" conta como perda (custo). Sai do Controle de Validade ao zerar.</summary>
    [HttpPost("{id:guid}/dar-destino")]
    public async Task<IActionResult> DarDestino(Guid id, [FromBody] DarDestinoRequest req, CancellationToken ct)
    {
        var destinos = new[] { "Descarte", "Devolucao", "UsoInterno" };
        if (!destinos.Contains(req.Destino))
            return BadRequest(new { mensagem = "Destino inválido (Descarte, Devolucao ou UsoInterno)." });

        var lote = await db.Lotes.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new KeyNotFoundException("Lote não encontrado.");
        if (lote.Quantidade <= 0) return BadRequest(new { mensagem = "Lote sem quantidade." });

        // Sem quantidade informada (ou maior que o saldo) = baixa tudo.
        var qtd = req.Quantidade > 0 && req.Quantidade < lote.Quantidade ? req.Quantidade : lote.Quantidade;

        var usuarioId = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid)
            ? uid : (Guid?)null;

        var mov = MovimentacaoEstoque.Criar(lote.EmpresaId, lote.ProdutoId, lote.LocalEstoqueId,
            TipoMovimentacao.Saida, qtd, lote.CustoUnitario,
            loteId: lote.Id, documentoOrigem: $"VENCIDO:{req.Destino}",
            usuarioId: usuarioId, observacao: req.Observacao);
        db.MovimentacoesEstoque.Add(mov);

        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == lote.ProdutoId, ct);
        produto?.AjustarEstoque(-qtd);

        lote.Baixar(qtd);
        await uow.SalvarAsync(ct);

        return Ok(new
        {
            loteId = id, baixado = qtd, restante = lote.Quantidade, destino = req.Destino,
            perda = req.Destino == "Descarte" ? qtd * lote.CustoUnitario : 0m
        });
    }

    /// <summary>Relatório de perdas/baixas por validade (movimentações "VENCIDO:*") no período.</summary>
    [HttpGet("perdas")]
    public async Task<IActionResult> Perdas([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] Guid? localEstoqueId, [FromQuery] string? destino, CancellationToken ct)
    {
        localEstoqueId = User.EscoparLoja(localEstoqueId);   // atendente: só a própria loja
        var fimExcl = fim.Date.AddDays(1);

        var q = db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId
                && m.DocumentoOrigem != null && m.DocumentoOrigem.StartsWith("VENCIDO:")
                && m.CriadoEm >= inicio.Date && m.CriadoEm < fimExcl);
        if (localEstoqueId.HasValue) q = q.Where(m => m.LocalEstoqueId == localEstoqueId);
        if (!string.IsNullOrWhiteSpace(destino)) q = q.Where(m => m.DocumentoOrigem == "VENCIDO:" + destino);

        var movs = await q.OrderByDescending(m => m.CriadoEm).ToListAsync(ct);

        var prodIds = movs.Select(m => m.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking().Where(p => prodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Codigo, p.Descricao }, ct);
        var lojaIds = movs.Select(m => m.LocalEstoqueId).Distinct().ToList();
        var lojas = await db.LocaisEstoque.AsNoTracking().Where(l => lojaIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Nome, ct);
        var userIds = movs.Where(m => m.UsuarioId.HasValue).Select(m => m.UsuarioId!.Value).Distinct().ToList();
        var usuarios = await db.Usuarios.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, ct);

        static string DestNome(string sufixo) => sufixo switch
        {
            "Descarte" => "Descarte / Perda",
            "Devolucao" => "Devolução ao fornecedor",
            "UsoInterno" => "Uso interno / reprocesso",
            _ => sufixo
        };

        var itens = movs.Select(m =>
        {
            var suf = m.DocumentoOrigem!.Replace("VENCIDO:", "");
            return new
            {
                data = m.CriadoEm,
                produto = produtos.TryGetValue(m.ProdutoId, out var p) ? p.Descricao : "Produto",
                codigo = produtos.TryGetValue(m.ProdutoId, out var pc) ? pc.Codigo : null,
                loja = lojas.GetValueOrDefault(m.LocalEstoqueId, "—"),
                destino = suf,
                destinoNome = DestNome(suf),
                quantidade = m.Quantidade,
                custoUnitario = m.CustoUnitario,
                custoTotal = m.Quantidade * m.CustoUnitario,
                usuario = m.UsuarioId.HasValue && usuarios.TryGetValue(m.UsuarioId.Value, out var un) ? un : null,
                observacao = m.Observacao
            };
        }).ToList();

        return Ok(new
        {
            itens,
            resumo = new
            {
                total = itens.Sum(i => i.custoTotal),
                perdaFinanceira = itens.Where(i => i.destino == "Descarte").Sum(i => i.custoTotal),
                quantidade = itens.Sum(i => i.quantidade),
                registros = itens.Count,
                porDestino = itens.GroupBy(i => i.destinoNome)
                    .Select(g => new { destino = g.Key, quantidade = g.Sum(x => x.quantidade), valor = g.Sum(x => x.custoTotal) })
                    .OrderByDescending(x => x.valor).ToList()
            }
        });
    }
}

public record DarDestinoRequest(string Destino, decimal Quantidade = 0, string? Observacao = null);

public record CriarLoteRequest(
    Guid EmpresaId, Guid ProdutoId, Guid LocalEstoqueId,
    string NumeroLote, decimal Quantidade, decimal CustoUnitario,
    DateTime? DataFabricacao = null, DateTime? DataValidade = null);

public record EditarLoteRequest(
    string NumeroLote, decimal Quantidade, decimal CustoUnitario,
    DateTime? DataFabricacao = null, DateTime? DataValidade = null);
