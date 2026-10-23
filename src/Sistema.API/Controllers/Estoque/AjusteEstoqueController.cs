using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/ajuste-estoque")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]   // ajuste de estoque não é para Atendente
public class AjusteEstoqueController(
    IProdutoRepository produtoRepo,
    IMovimentacaoEstoqueRepository movRepo,
    SistemaDbContext db,
    IUnitOfWork uow) : ControllerBase
{
    // Estoque reconstruído POR LOJA a partir das movimentações — MESMA regra da requisição de compra
    // (Entrada/AjustePositivo/Devolução somam; Saída/AjusteNegativo/Transferência subtraem).
    private async Task<decimal> EstoqueNaLojaAsync(Guid empresaId, Guid produtoId, Guid loja, CancellationToken ct)
    {
        var sinais = await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.ProdutoId == produtoId && m.LocalEstoqueId == loja)
            .Select(m =>
                m.Tipo == TipoMovimentacao.Entrada || m.Tipo == TipoMovimentacao.AjustePositivo || m.Tipo == TipoMovimentacao.Devolucao ? m.Quantidade
              : m.Tipo == TipoMovimentacao.Saida || m.Tipo == TipoMovimentacao.AjusteNegativo || m.Tipo == TipoMovimentacao.Transferencia ? -m.Quantidade
              : 0m)
            .ToListAsync(ct);
        return sinais.Sum();
    }

    private async Task<Dictionary<Guid, decimal>> EstoquePorProdutoNaLojaAsync(Guid empresaId, Guid loja, CancellationToken ct)
        => (await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.LocalEstoqueId == loja)
            .GroupBy(m => m.ProdutoId)
            .Select(g => new
            {
                ProdutoId = g.Key,
                Saldo = g.Sum(m =>
                    m.Tipo == TipoMovimentacao.Entrada || m.Tipo == TipoMovimentacao.AjustePositivo || m.Tipo == TipoMovimentacao.Devolucao ? m.Quantidade
                  : m.Tipo == TipoMovimentacao.Saida || m.Tipo == TipoMovimentacao.AjusteNegativo || m.Tipo == TipoMovimentacao.Transferencia ? -m.Quantidade
                  : 0m),
            })
            .ToListAsync(ct))
            .ToDictionary(x => x.ProdutoId, x => x.Saldo);

    /// <summary>Estoque DA LOJA (reconstruído) de um produto — para a tela mostrar a base certa.</summary>
    [HttpGet("estoque-loja")]
    public async Task<IActionResult> EstoqueLoja([FromQuery] Guid empresaId, [FromQuery] Guid produtoId,
        [FromQuery] Guid localEstoqueId, CancellationToken ct)
        => Ok(new { estoque = await EstoqueNaLojaAsync(empresaId, produtoId, localEstoqueId, ct) });

    /// <summary>Lista de produtos ativos com o estoque DA LOJA (para o inventário em lote).</summary>
    [HttpGet("inventario-loja")]
    public async Task<IActionResult> InventarioLoja([FromQuery] Guid empresaId, [FromQuery] Guid localEstoqueId, CancellationToken ct)
    {
        var saldo = await EstoquePorProdutoNaLojaAsync(empresaId, localEstoqueId, ct);
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo)
            .Select(p => new { p.Id, p.Codigo, p.Descricao, p.EstoqueMinimo })
            .OrderBy(p => p.Descricao)
            .ToListAsync(ct);

        var itens = produtos.Select(p => new
        {
            id = p.Id,
            codigo = p.Codigo,
            descricao = p.Descricao,
            estoqueMinimo = p.EstoqueMinimo,
            estoqueAtual = saldo.GetValueOrDefault(p.Id, 0m),   // estoque DA LOJA (não o global)
        }).ToList();

        return Ok(itens);
    }

    /// <summary>Ajuste unitário — define a quantidade exata do produto NA LOJA.</summary>
    [HttpPost("unitario")]
    public async Task<IActionResult> Unitario([FromBody] AjusteUnitarioRequest req, CancellationToken ct)
    {
        var produto = await produtoRepo.ObterPorIdAsync(req.ProdutoId, ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        var estoqueLoja = await EstoqueNaLojaAsync(req.EmpresaId, req.ProdutoId, req.LocalEstoqueId, ct);
        var diferenca = req.QuantidadeContada - estoqueLoja;   // base = estoque DA LOJA (não o global)
        if (diferenca == 0) return NoContent();

        var tipo = diferenca > 0 ? TipoMovimentacao.AjustePositivo : TipoMovimentacao.AjusteNegativo;
        var qtdAbsoluta = Math.Abs(diferenca);

        var mov = MovimentacaoEstoque.Criar(
            req.EmpresaId, req.ProdutoId, req.LocalEstoqueId,
            tipo, qtdAbsoluta, produto.CustoUnitario,
            documentoOrigem: "AJUSTE", usuarioId: req.UsuarioId, observacao: req.Observacao);

        produto.AjustarEstoque(diferenca);   // global acompanha a mesma diferença
        await movRepo.AdicionarAsync(mov, ct);
        produtoRepo.Atualizar(produto);
        await uow.SalvarAsync(ct);

        return Ok(new
        {
            produtoId = req.ProdutoId,
            estoqueAnterior = estoqueLoja,
            estoqueAtual = estoqueLoja + diferenca,
            diferenca,
            tipo = tipo.ToString()
        });
    }

    /// <summary>Ajuste em lote — processa vários produtos de uma vez (inventário) NA LOJA.</summary>
    [HttpPost("lote")]
    public async Task<IActionResult> EmLote([FromBody] AjusteLoteRequest req, CancellationToken ct)
    {
        var resultados = new List<object>();
        var saldoLoja = await EstoquePorProdutoNaLojaAsync(req.EmpresaId, req.LocalEstoqueId, ct);

        foreach (var item in req.Itens)
        {
            var produto = await produtoRepo.ObterPorIdAsync(item.ProdutoId, ct);
            if (produto is null)
            {
                resultados.Add(new { item.ProdutoId, erro = "Produto não encontrado" });
                continue;
            }

            var estoqueLoja = saldoLoja.GetValueOrDefault(item.ProdutoId, 0m);
            var diferenca = item.QuantidadeContada - estoqueLoja;   // base = estoque DA LOJA
            if (diferenca == 0)
            {
                resultados.Add(new { item.ProdutoId, produto.Descricao, diferenca = 0m, status = "Sem diferença" });
                continue;
            }

            var tipo = diferenca > 0 ? TipoMovimentacao.AjustePositivo : TipoMovimentacao.AjusteNegativo;
            var mov = MovimentacaoEstoque.Criar(
                req.EmpresaId, item.ProdutoId, req.LocalEstoqueId,
                tipo, Math.Abs(diferenca), produto.CustoUnitario,
                documentoOrigem: $"INVENTARIO-{DateTime.Today:yyyyMMdd}", usuarioId: req.UsuarioId);

            produto.AjustarEstoque(diferenca);
            await movRepo.AdicionarAsync(mov, ct);
            produtoRepo.Atualizar(produto);

            resultados.Add(new
            {
                item.ProdutoId, produto.Descricao,
                estoqueAnterior = estoqueLoja,
                item.QuantidadeContada, diferenca,
                tipo = tipo.ToString(), status = "Ajustado"
            });
        }

        await uow.SalvarAsync(ct);
        return Ok(new { processados = resultados.Count, resultados });
    }
}

public record AjusteUnitarioRequest(
    Guid EmpresaId, Guid ProdutoId, Guid LocalEstoqueId,
    decimal QuantidadeContada, Guid? UsuarioId = null, string? Observacao = null);

public record ItemAjusteLote(Guid ProdutoId, decimal QuantidadeContada);

public record AjusteLoteRequest(
    Guid EmpresaId, Guid LocalEstoqueId,
    IList<ItemAjusteLote> Itens, Guid? UsuarioId = null);
