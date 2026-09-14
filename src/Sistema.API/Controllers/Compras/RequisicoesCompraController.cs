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

    /// <summary>
    /// Pendências de compra para consolidação: itens das requisições ABERTAS + dos pedidos
    /// de compra em RASCUNHO (não processados), agrupados por loja (produto × quantidade).
    /// Mostra ao gestor o que está solto para reunir numa requisição única por loja.
    /// </summary>
    [HttpGet("pendencias")]
    public async Task<IActionResult> Pendencias([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var reqAbertas = await db.RequisicoesCompra.AsNoTracking()
            .Where(r => r.EmpresaId == empresaId && r.Status == StatusRequisicaoCompra.Aberta)
            .Select(r => new { r.Id, r.LocalEstoqueId }).ToListAsync(ct);
        var pedRascunho = await db.PedidosCompra.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Status == StatusPedidoCompra.Rascunho)
            .Select(p => new { p.Id, p.Numero, p.LocalEstoqueId }).ToListAsync(ct);

        var reqIds = reqAbertas.Select(x => x.Id).ToList();
        var pedIds = pedRascunho.Select(x => x.Id).ToList();
        var reqLoja = reqAbertas.ToDictionary(x => x.Id, x => x.LocalEstoqueId);
        var pedLoja = pedRascunho.ToDictionary(x => x.Id, x => x.LocalEstoqueId);

        var itensReq = await db.ItensRequisicaoCompra.AsNoTracking()
            .Where(i => reqIds.Contains(i.RequisicaoCompraId))
            .Select(i => new { i.RequisicaoCompraId, i.ProdutoId, i.Descricao, i.Quantidade }).ToListAsync(ct);
        var itensPed = await db.ItensPedidoCompra.AsNoTracking()
            .Where(i => pedIds.Contains(i.PedidoCompraId))
            .Select(i => new { i.PedidoCompraId, i.ProdutoId, i.Descricao, i.Quantidade }).ToListAsync(ct);

        var todos = itensReq
            .Select(i => new { Loja = reqLoja.GetValueOrDefault(i.RequisicaoCompraId), i.ProdutoId, i.Descricao, i.Quantidade })
            .Concat(itensPed.Select(i => new { Loja = pedLoja.GetValueOrDefault(i.PedidoCompraId), i.ProdutoId, i.Descricao, i.Quantidade }))
            .Where(x => x.Loja != null)
            .ToList();

        var lojaIds = todos.Select(x => x.Loja!.Value).Distinct().ToList();
        var lojas = await db.LocaisEstoque.AsNoTracking().Where(l => lojaIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Nome }).ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        var porLoja = todos.GroupBy(x => x.Loja!.Value).Select(g => new
        {
            localEstoqueId = g.Key,
            loja = lojas.GetValueOrDefault(g.Key, "—"),
            itens = g.GroupBy(y => y.ProdutoId).Select(gp => new
            {
                produtoId = gp.Key,
                descricao = gp.First().Descricao,
                quantidade = gp.Sum(z => z.Quantidade),
            }).OrderBy(i => i.descricao).ToList(),
        }).OrderBy(x => x.loja).ToList();

        return Ok(new
        {
            requisicoesAbertas = reqAbertas.Count,
            pedidosRascunho = pedRascunho.Count,
            temPendencias = porLoja.Count > 0,
            porLoja,
        });
    }

    /// <summary>
    /// Consolida as pendências: cria UMA requisição de compra (Aberta) por loja com os itens
    /// das requisições abertas + pedidos em rascunho, e CANCELA essas origens (para não duplicar).
    /// Só gestor. Espelha a limpeza manual que reúne tudo num único lugar por loja.
    /// </summary>
    [HttpPost("consolidar")]
    public async Task<IActionResult> Consolidar([FromBody] ConsolidarRequest req, CancellationToken ct)
    {
        if (User.IsInRole("Atendente"))
            return Forbid();

        var empresaId = req.EmpresaId;
        var abertas = await db.RequisicoesCompra
            .Where(r => r.EmpresaId == empresaId && r.Status == StatusRequisicaoCompra.Aberta).ToListAsync(ct);
        var rascunhos = await db.PedidosCompra
            .Where(p => p.EmpresaId == empresaId && p.Status == StatusPedidoCompra.Rascunho).ToListAsync(ct);

        var reqIds = abertas.Select(r => r.Id).ToList();
        var pedIds = rascunhos.Select(p => p.Id).ToList();
        var reqLoja = abertas.ToDictionary(r => r.Id, r => r.LocalEstoqueId);
        var pedLoja = rascunhos.ToDictionary(p => p.Id, p => p.LocalEstoqueId);

        var itensReq = await db.ItensRequisicaoCompra.AsNoTracking()
            .Where(i => reqIds.Contains(i.RequisicaoCompraId)).ToListAsync(ct);
        var itensPed = await db.ItensPedidoCompra.AsNoTracking()
            .Where(i => pedIds.Contains(i.PedidoCompraId)).ToListAsync(ct);

        var todos = itensReq
            .Select(i => new { Loja = reqLoja.GetValueOrDefault(i.RequisicaoCompraId), i.ProdutoId, i.Descricao, i.Quantidade })
            .Concat(itensPed.Select(i => new { Loja = pedLoja.GetValueOrDefault(i.PedidoCompraId), i.ProdutoId, i.Descricao, i.Quantidade }))
            .Where(x => x.Loja != null)
            .ToList();

        if (todos.Count == 0)
            return BadRequest(new { mensagem = "Não há pendências (requisições abertas ou pedidos em rascunho) para consolidar." });

        var criadas = new List<object>();
        foreach (var g in todos.GroupBy(x => x.Loja!.Value))
        {
            var nova = RequisicaoCompra.Criar(empresaId, req.UsuarioId, g.Key,
                $"Consolidada em {DateTime.Now:dd/MM/yyyy} (requisições abertas + pedidos em rascunho)");
            foreach (var it in g.GroupBy(y => y.ProdutoId))
                nova.AdicionarItem(it.Key, it.First().Descricao, it.Sum(z => z.Quantidade));
            db.RequisicoesCompra.Add(nova);
            criadas.Add(new { nova.Id, localEstoqueId = g.Key, itens = g.Select(y => y.ProdutoId).Distinct().Count() });
        }

        foreach (var r in abertas) r.Cancelar();
        foreach (var p in rascunhos) p.Cancelar();

        await uow.SalvarAsync(ct);
        return Ok(new
        {
            mensagem = $"{criadas.Count} requisição(ões) consolidada(s). {abertas.Count} requisição(ões) e {rascunhos.Count} pedido(s) em rascunho foram cancelados.",
            criadas,
        });
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

        // Fornecedores: principais dos produtos + os escolhidos por item (override).
        var fornIds = produtos.Where(p => p.FornecedorPrincipalId.HasValue)
            .Select(p => p.FornecedorPrincipalId!.Value)
            .Concat(itens.Where(i => i.FornecedorId.HasValue).Select(i => i.FornecedorId!.Value))
            .Distinct().ToList();
        var forns = await db.Fornecedores.AsNoTracking().Where(f => fornIds.Contains(f.Id))
            .Select(f => new { f.Id, f.RazaoSocial }).ToDictionaryAsync(f => f.Id, f => f.RazaoSocial, ct);

        return Ok(new
        {
            req.Id, status = req.Status.ToString(), req.Observacao, req.LocalEstoqueId,
            criadoEm = DateTime.SpecifyKind(req.CriadoEm, DateTimeKind.Utc).ToLocalTime(),
            itens = itens.Select(i =>
            {
                var p = pmap.GetValueOrDefault(i.ProdutoId);
                // Fornecedor efetivo = o escolhido no item (override) ou o principal do produto.
                var effForn = i.FornecedorId ?? p?.FornecedorPrincipalId;
                return new
                {
                    itemId = i.Id,
                    produtoId = i.ProdutoId,
                    descricao = i.Descricao,
                    quantidade = i.Quantidade,
                    custoUnitario = p?.CustoUnitario ?? 0m,
                    fornecedorId = effForn,
                    fornecedor = effForn is Guid fid && forns.TryGetValue(fid, out var fn)
                        ? fn : "(sem fornecedor)",
                    movido = i.FornecedorId.HasValue,
                };
            }).ToList()
        });
    }

    /// <summary>
    /// Confere o que foi requisitado × o que já foi pedido (pedidos de compra).
    /// Usa o vínculo pedido→requisição quando existe; senão, cai para os pedidos da
    /// mesma loja criados a partir da data da requisição (aproximado, para dados antigos).
    /// </summary>
    [HttpGet("{id:guid}/conferencia")]
    public async Task<IActionResult> Conferencia(Guid id, CancellationToken ct)
    {
        var req = await db.RequisicoesCompra.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.EmpresaId, r.LocalEstoqueId, r.CriadoEm })
            .FirstOrDefaultAsync(ct);
        if (req is null) return NotFound();

        var itens = await db.ItensRequisicaoCompra.AsNoTracking()
            .Where(i => i.RequisicaoCompraId == id)
            .Select(i => new { i.ProdutoId, i.Descricao, i.Quantidade })
            .ToListAsync(ct);

        // "Vai chegar" = o produto DEU ENTRADA no estoque desta loja a partir da data da
        // requisição (NF processada / recebimento). Independe de qual OC/requisição casou a NF,
        // refletindo a chegada física real. O resto fica "aguardando fornecedor".
        var reqProdutoIds = itens.Select(i => i.ProdutoId).ToList();
        var chegaram = await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == req.EmpresaId
                && m.LocalEstoqueId == req.LocalEstoqueId
                && m.Tipo == Sistema.Domain.Estoque.Entities.TipoMovimentacao.Entrada
                && m.CriadoEm >= req.CriadoEm
                && reqProdutoIds.Contains(m.ProdutoId))
            .Select(m => m.ProdutoId).Distinct().ToListAsync(ct);
        var chegaramSet = chegaram.ToHashSet();

        // TODOS os pedidos NÃO cancelados da MESMA loja (vinculados ou não, Rascunho/Enviado/
        // Recebido) — para detectar se o item já está em algum pedido e evitar pedir duplicado.
        var pedidosLoja = await db.PedidosCompra.AsNoTracking()
            .Where(p => p.EmpresaId == req.EmpresaId
                && p.LocalEstoqueId == req.LocalEstoqueId
                && p.Status != StatusPedidoCompra.Cancelado)
            .Select(p => new { p.Id, p.Numero, Status = p.Status.ToString() }).ToListAsync(ct);

        var pedidoIds = pedidosLoja.Select(p => p.Id).ToList();
        var infoPorId = pedidosLoja.ToDictionary(p => p.Id, p => new { p.Numero, p.Status });

        var itensPedido = await db.ItensPedidoCompra.AsNoTracking()
            .Where(i => pedidoIds.Contains(i.PedidoCompraId))
            .Select(i => new { i.PedidoCompraId, i.ProdutoId, i.Quantidade })
            .ToListAsync(ct);

        var linhas = itens.Select(it =>
        {
            var casados = itensPedido.Where(ip => ip.ProdutoId == it.ProdutoId).ToList();
            var pedido = casados.Sum(c => c.Quantidade);
            var pedidosDoItem = casados
                .Select(c => infoPorId.GetValueOrDefault(c.PedidoCompraId))
                .Where(x => x is not null)
                .GroupBy(x => x!.Numero)
                .Select(gg => new { numero = gg.Key, status = gg.First()!.Status })
                .OrderBy(x => x.numero).ToList();
            var vaiChegar = chegaramSet.Contains(it.ProdutoId);
            var jaPedido = pedido > 0;
            return new
            {
                produtoId = it.ProdutoId,
                descricao = it.Descricao,
                requisitado = it.Quantidade,
                pedido,
                pendente = Math.Max(0, it.Quantidade - pedido),
                jaPedido,
                pedidos = pedidosDoItem,
                situacao = vaiChegar ? "VaiChegar" : (jaPedido ? "JaPedido" : "Aguardando"),
            };
        }).ToList();

        return Ok(new
        {
            requisicaoId = id,
            aproximado = false,
            totalItens = linhas.Count,
            jaEmPedido = linhas.Count(l => l.jaPedido),
            itensPendentes = linhas.Count(l => !l.jaPedido && l.situacao != "VaiChegar"),
            vaoChegar = linhas.Count(l => l.situacao == "VaiChegar"),
            completo = linhas.All(l => l.situacao == "VaiChegar" || l.jaPedido),
            itens = linhas,
        });
    }

    /// <summary>Move um item da requisição para outro fornecedor (override do fornecedor
    /// principal do produto). Passe fornecedorId = null para voltar ao principal. Só gestor.</summary>
    [HttpPatch("itens/{itemId:guid}/fornecedor")]
    public async Task<IActionResult> MoverItemFornecedor(Guid itemId, [FromBody] MoverItemFornecedorRequest req, CancellationToken ct)
    {
        if (User.IsInRole("Atendente")) return Forbid();
        var item = await db.ItensRequisicaoCompra.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return NotFound();
        item.DefinirFornecedor(req.FornecedorId);
        await uow.SalvarAsync(ct);
        return NoContent();
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

    /// <summary>Exclui a requisição (e seus itens). Uso: limpar requisições de teste/erradas.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var req = await db.RequisicoesCompra.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Requisição não encontrada.");
        db.RequisicoesCompra.Remove(req);   // itens caem por cascade
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarRequisicaoRequest(
    Guid EmpresaId, Guid UsuarioId, Guid? LocalEstoqueId, string? Observacao,
    List<ItemRequisicaoRequest> Itens);

public record ItemRequisicaoRequest(Guid ProdutoId, decimal Quantidade);

public record ConsolidarRequest(Guid EmpresaId, Guid UsuarioId);

public record MoverItemFornecedorRequest(Guid? FornecedorId);
