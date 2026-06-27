using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/ajuste-estoque")]
[Authorize]
public class AjusteEstoqueController(
    IProdutoRepository produtoRepo,
    IMovimentacaoEstoqueRepository movRepo,
    IUnitOfWork uow) : ControllerBase
{
    /// <summary>Ajuste unitário — define a quantidade exata do produto.</summary>
    [HttpPost("unitario")]
    public async Task<IActionResult> Unitario([FromBody] AjusteUnitarioRequest req, CancellationToken ct)
    {
        var produto = await produtoRepo.ObterPorIdAsync(req.ProdutoId, ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        var diferenca = req.QuantidadeContada - produto.EstoqueAtual;
        if (diferenca == 0) return NoContent();

        var tipo = diferenca > 0 ? TipoMovimentacao.AjustePositivo : TipoMovimentacao.AjusteNegativo;
        var qtdAbsoluta = Math.Abs(diferenca);

        var mov = MovimentacaoEstoque.Criar(
            req.EmpresaId, req.ProdutoId, req.LocalEstoqueId,
            tipo, qtdAbsoluta, produto.CustoUnitario,
            documentoOrigem: "AJUSTE", usuarioId: req.UsuarioId, observacao: req.Observacao);

        produto.AjustarEstoque(diferenca);
        await movRepo.AdicionarAsync(mov, ct);
        produtoRepo.Atualizar(produto);
        await uow.SalvarAsync(ct);

        return Ok(new
        {
            produtoId = req.ProdutoId,
            estoqueAnterior = req.QuantidadeContada - diferenca,
            estoqueAtual = produto.EstoqueAtual,
            diferenca,
            tipo = tipo.ToString()
        });
    }

    /// <summary>Ajuste em lote — processa vários produtos de uma vez (inventário).</summary>
    [HttpPost("lote")]
    public async Task<IActionResult> EmLote([FromBody] AjusteLoteRequest req, CancellationToken ct)
    {
        var resultados = new List<object>();

        foreach (var item in req.Itens)
        {
            var produto = await produtoRepo.ObterPorIdAsync(item.ProdutoId, ct);
            if (produto is null)
            {
                resultados.Add(new { item.ProdutoId, erro = "Produto não encontrado" });
                continue;
            }

            var diferenca = item.QuantidadeContada - produto.EstoqueAtual;
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
                estoqueAnterior = item.QuantidadeContada - diferenca,
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
