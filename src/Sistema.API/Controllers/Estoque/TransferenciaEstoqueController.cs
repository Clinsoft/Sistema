using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/transferencias")]
[Authorize]
public class TransferenciaEstoqueController(
    IProdutoRepository produtoRepo,
    IMovimentacaoEstoqueRepository movRepo,
    IUnitOfWork uow) : ControllerBase
{
    /// <summary>Transfere quantidade de um local de estoque para outro.</summary>
    [HttpPost]
    public async Task<IActionResult> Transferir([FromBody] TransferenciaRequest req, CancellationToken ct)
    {
        if (req.LocalOrigemId == req.LocalDestinoId)
            throw new InvalidOperationException("Origem e destino não podem ser iguais.");

        var produto = await produtoRepo.ObterPorIdAsync(req.ProdutoId, ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        if (produto.EstoqueAtual < req.Quantidade)
            throw new InvalidOperationException($"Estoque insuficiente. Disponível: {produto.EstoqueAtual}.");

        var saida = MovimentacaoEstoque.Criar(
            req.EmpresaId, req.ProdutoId, req.LocalOrigemId,
            TipoMovimentacao.Transferencia, req.Quantidade, produto.CustoUnitario,
            documentoOrigem: $"TRANSF->{req.LocalDestinoId}",
            usuarioId: req.UsuarioId, observacao: req.Observacao);

        var entrada = MovimentacaoEstoque.Criar(
            req.EmpresaId, req.ProdutoId, req.LocalDestinoId,
            TipoMovimentacao.Transferencia, req.Quantidade, produto.CustoUnitario,
            documentoOrigem: $"TRANSF<-{req.LocalOrigemId}",
            usuarioId: req.UsuarioId, observacao: req.Observacao);

        await movRepo.AdicionarAsync(saida, ct);
        await movRepo.AdicionarAsync(entrada, ct);
        await uow.SalvarAsync(ct);

        return Ok(new { saidaId = saida.Id, entradaId = entrada.Id });
    }
}

public record TransferenciaRequest(
    Guid EmpresaId, Guid ProdutoId,
    Guid LocalOrigemId, Guid LocalDestinoId,
    decimal Quantidade, Guid? UsuarioId = null, string? Observacao = null);
