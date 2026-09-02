using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/transferencias")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]   // transferência de estoque não é para Atendente
public class TransferenciaEstoqueController(
    IProdutoRepository produtoRepo,
    IMovimentacaoEstoqueRepository movRepo,
    SistemaDbContext db,
    IUnitOfWork uow) : ControllerBase
{
    /// <summary>Transfere quantidade entre locais de estoque da mesma empresa.</summary>
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

    /// <summary>
    /// Transfere um lote (total ou parcial) de uma filial para outra.
    /// Gera saída na empresa origem e entrada na empresa destino,
    /// ambas com tipo Transferencia e referência cruzada no documentoOrigem.
    /// </summary>
    [HttpPost("filial")]
    public async Task<IActionResult> TransferirFilial(
        [FromBody] TransferenciaFilialRequest req, CancellationToken ct)
    {
        // Valida lote origem
        var lote = await db.Lotes.FirstOrDefaultAsync(
            l => l.Id == req.LoteId && l.EmpresaId == req.EmpresaOrigemId, ct)
            ?? throw new KeyNotFoundException("Lote não encontrado.");

        if (req.Quantidade <= 0 || req.Quantidade > lote.Quantidade)
            throw new InvalidOperationException(
                $"Quantidade inválida. Disponível no lote: {lote.Quantidade}.");

        // Valida que destino é filial do mesmo grupo
        var origem  = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == req.EmpresaOrigemId, ct)
            ?? throw new KeyNotFoundException("Empresa origem não encontrada.");
        var destino = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == req.EmpresaDestinoId, ct)
            ?? throw new KeyNotFoundException("Empresa destino não encontrada.");

        var matrizOrigem  = origem.MatrizId  ?? origem.Id;
        var matrizDestino = destino.MatrizId ?? destino.Id;
        if (matrizOrigem != matrizDestino)
            throw new InvalidOperationException("Empresas não pertencem ao mesmo grupo.");

        // Produto na empresa destino (pode ter id diferente se cadastrado separado)
        var produtoOrigem  = await db.Produtos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == lote.ProdutoId, ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        // Tenta encontrar o produto correspondente na filial pelo código de barras ou código
        var produtoDestino = await db.Produtos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmpresaId == req.EmpresaDestinoId
                && (p.CodigoBarras == produtoOrigem.CodigoBarras || p.Codigo == produtoOrigem.Codigo), ct);

        var protocolo = $"TRANSF-FILIAL-{DateTime.UtcNow:yyyyMMddHHmmss}";

        // ── Saída na origem ──────────────────────────────────────────
        var localOrigemId = req.LocalOrigemId
            ?? (await db.LocaisEstoque.AsNoTracking()
               .Where(l => l.EmpresaId == req.EmpresaOrigemId)
               .Select(l => (Guid?)l.Id)
               .FirstOrDefaultAsync(ct));

        var saida = MovimentacaoEstoque.Criar(
            req.EmpresaOrigemId, lote.ProdutoId, localOrigemId ?? lote.LocalEstoqueId,
            TipoMovimentacao.Transferencia, req.Quantidade, produtoOrigem.CustoUnitario,
            loteId: lote.Id,
            documentoOrigem: $"{protocolo}->FILIAL:{req.EmpresaDestinoId}",
            usuarioId: req.UsuarioId,
            observacao: req.Observacao ?? $"Transferência para {destino.NomeFantasia}");

        db.MovimentacoesEstoque.Add(saida);

        // Atualiza quantidade do lote origem
        lote.AtualizarQuantidade(lote.Quantidade - req.Quantidade);

        // ── Entrada na destino ────────────────────────────────────────
        if (produtoDestino is not null)
        {
            var localDestinoId = req.LocalDestinoId
                ?? (await db.LocaisEstoque.AsNoTracking()
                   .Where(l => l.EmpresaId == req.EmpresaDestinoId)
                   .Select(l => (Guid?)l.Id)
                   .FirstOrDefaultAsync(ct));

            // Cria ou atualiza lote na filial destino
            var loteDestino = await db.Lotes.FirstOrDefaultAsync(
                l => l.EmpresaId == req.EmpresaDestinoId
                  && l.ProdutoId == produtoDestino.Id
                  && l.NumeroLote == lote.NumeroLote, ct);

            if (loteDestino is null)
            {
                loteDestino = Lote.Criar(
                    req.EmpresaDestinoId, produtoDestino.Id,
                    localDestinoId ?? lote.LocalEstoqueId,
                    lote.NumeroLote ?? protocolo,
                    req.Quantidade, produtoOrigem.CustoUnitario,
                    dataFabricacao: lote.DataFabricacao,
                    dataValidade: lote.DataValidade);
                db.Lotes.Add(loteDestino);
            }
            else
            {
                loteDestino.AtualizarQuantidade(loteDestino.Quantidade + req.Quantidade);
            }

            var entrada = MovimentacaoEstoque.Criar(
                req.EmpresaDestinoId, produtoDestino.Id,
                localDestinoId ?? lote.LocalEstoqueId,
                TipoMovimentacao.Transferencia, req.Quantidade, produtoOrigem.CustoUnitario,
                loteId: loteDestino.Id,
                documentoOrigem: $"{protocolo}<-FILIAL:{req.EmpresaOrigemId}",
                usuarioId: req.UsuarioId,
                observacao: req.Observacao ?? $"Recebido de {origem.NomeFantasia}");

            db.MovimentacoesEstoque.Add(entrada);
        }

        await uow.SalvarAsync(ct);

        return Ok(new
        {
            protocolo,
            mensagem = produtoDestino is null
                ? $"Saída registrada. Produto não encontrado na filial destino — cadastre-o lá para registrar a entrada automaticamente."
                : "Transferência concluída com sucesso.",
            quantidadeTransferida = req.Quantidade,
            saldoRestante = lote.Quantidade,
        });
    }

    /// <summary>Lista as filiais do mesmo grupo disponíveis para transferência.</summary>
    [HttpGet("filiais-destino")]
    public async Task<IActionResult> FiliaisDestino(
        [FromQuery] Guid empresaOrigemId, CancellationToken ct)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empresaOrigemId, ct);
        if (empresa is null) return NotFound();

        var matrizId = empresa.MatrizId ?? empresa.Id;

        var grupo = await db.Empresas.AsNoTracking()
            .Where(e => (e.Id == matrizId || e.MatrizId == matrizId)
                     && e.Id != empresaOrigemId && e.Ativo)
            .Select(e => new { e.Id, e.NomeFantasia, e.TipoUnidade })
            .ToListAsync(ct);

        return Ok(grupo);
    }
}

public record TransferenciaRequest(
    Guid EmpresaId, Guid ProdutoId,
    Guid LocalOrigemId, Guid LocalDestinoId,
    decimal Quantidade, Guid? UsuarioId = null, string? Observacao = null);

public record TransferenciaFilialRequest(
    Guid EmpresaOrigemId, Guid EmpresaDestinoId, Guid LoteId,
    decimal Quantidade,
    Guid? LocalOrigemId = null, Guid? LocalDestinoId = null,
    Guid? UsuarioId = null, string? Observacao = null);
