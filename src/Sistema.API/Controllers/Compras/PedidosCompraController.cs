using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Compras.Commands;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Compras.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;
using UglyToad.PdfPig;

namespace Sistema.API.Controllers.Compras;

[ApiController]
[Route("api/pedidos-compra")]
[Authorize]
public class PedidosCompraController(IMediator mediator, IPedidoCompraRepository repo, SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarPedidoCompraCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);

        // Ao gerar o pedido a partir de uma requisição, marca a requisição como Processada.
        if (cmd.RequisicaoCompraId is Guid reqId)
        {
            var req = await db.RequisicoesCompra.FirstOrDefaultAsync(r => r.Id == reqId, ct);
            if (req is not null && req.Status == Sistema.Domain.Compras.Entities.StatusRequisicaoCompra.Aberta)
            {
                req.Processar();
                await uow.SalvarAsync(ct);
            }
        }
        return Ok(new { id });
    }

    [HttpPost("{id:guid}/enviar")]
    public async Task<IActionResult> Enviar(Guid id, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null || pedido.EmpresaId != empresaId) return NotFound();
        pedido.Enviar();
        repo.Atualizar(pedido);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/receber")]
    public async Task<IActionResult> Receber(Guid id, [FromBody] ReceberRequest req, CancellationToken ct)
    {
        var itens = req.Itens?
            .Where(i => i.ProdutoId != Guid.Empty && i.Quantidade >= 0)
            .Select(i => new ItemRecebido(i.ProdutoId, i.Quantidade))
            .ToList();
        var r = await mediator.Send(new ReceberPedidoCompraCommand(id, req.LocalEstoqueId, req.UsuarioId, itens), ct);
        return Ok(new { rascunhoNumero = r.RascunhoNumero, faltantes = r.Faltantes });
    }

    /// <summary>Define o fornecedor de um pedido (ex.: pedido de faltantes "a definir").</summary>
    [HttpPatch("{id:guid}/fornecedor")]
    public async Task<IActionResult> DefinirFornecedor(Guid id, [FromBody] DefinirFornecedorRequest req, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null) return NotFound();
        pedido.DefinirFornecedor(req.FornecedorId);
        repo.Atualizar(pedido);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Define a unidade (loja) de entrega do pedido.</summary>
    [HttpPatch("{id:guid}/loja")]
    public async Task<IActionResult> DefinirLoja(Guid id, [FromBody] DefinirLojaRequest req, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null) return NotFound();
        pedido.DefinirLocalEstoque(req.LocalEstoqueId);
        repo.Atualizar(pedido);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null || pedido.EmpresaId != empresaId) return NotFound();
        pedido.Cancelar();
        repo.Atualizar(pedido);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Exclui um pedido de compra definitivamente (e seus itens). Bloqueado para pedidos
    /// já RECEBIDOS ou vinculados a uma escrituração de entrada. Só gestor.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        if (User.IsInRole("Atendente")) return Forbid();

        var pedido = await db.PedidosCompra.FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == empresaId, ct);
        if (pedido is null) return NotFound();

        if (pedido.Status == StatusPedidoCompra.Recebido)
            return BadRequest(new { mensagem = "Não é possível excluir um pedido já recebido — ele faz parte do histórico de entrada." });

        var temEscrituracao = await db.EntradasNFe.AnyAsync(e => e.PedidoCompraId == id, ct);
        if (temEscrituracao)
            return BadRequest(new { mensagem = "Este pedido está vinculado a uma escrituração de entrada. Desvincule antes de excluir." });

        var itens = await db.ItensPedidoCompra.Where(i => i.PedidoCompraId == id).ToListAsync(ct);
        db.ItensPedidoCompra.RemoveRange(itens);
        db.PedidosCompra.Remove(pedido);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Une vários pedidos de compra (em RASCUNHO, do MESMO fornecedor) em um único pedido:
    /// soma as quantidades por produto (preço = média ponderada) e remove os originais.
    /// Só gestor.
    /// </summary>
    [HttpPost("unir")]
    public async Task<IActionResult> Unir([FromBody] UnirPedidosRequest req, CancellationToken ct)
    {
        if (User.IsInRole("Atendente")) return Forbid();
        if (req.PedidoIds is null || req.PedidoIds.Count < 2)
            return BadRequest(new { mensagem = "Selecione ao menos 2 pedidos para unir." });

        var pedidos = await db.PedidosCompra
            .Where(p => p.EmpresaId == req.EmpresaId && req.PedidoIds.Contains(p.Id))
            .ToListAsync(ct);
        if (pedidos.Count < 2)
            return BadRequest(new { mensagem = "Pedidos não encontrados para unir." });

        if (pedidos.Any(p => p.Status != StatusPedidoCompra.Rascunho && p.Status != StatusPedidoCompra.Enviado))
            return BadRequest(new { mensagem = "Só é possível unir pedidos em Rascunho ou Enviado (não recebidos nem cancelados)." });

        if (pedidos.Select(p => p.FornecedorId).Distinct().Count() > 1)
            return BadRequest(new { mensagem = "Os pedidos têm fornecedores diferentes — um pedido de compra tem um único fornecedor. Una apenas pedidos do mesmo fornecedor." });

        var fornecedorId = pedidos[0].FornecedorId;
        var lojas = pedidos.Select(p => p.LocalEstoqueId).Distinct().ToList();
        var lojaId = lojas.Count == 1 ? lojas[0] : pedidos.FirstOrDefault(p => p.LocalEstoqueId != null)?.LocalEstoqueId;

        var ids = pedidos.Select(p => p.Id).ToList();
        var itens = await db.ItensPedidoCompra.AsNoTracking()
            .Where(i => ids.Contains(i.PedidoCompraId)).ToListAsync(ct);

        var numero = await repo.ProximoNumeroAsync(req.EmpresaId, ct);
        var novo = PedidoCompra.Criar(req.EmpresaId, fornecedorId, req.UsuarioId, numero, null, lojaId);
        foreach (var g in itens.GroupBy(i => i.ProdutoId))
        {
            var qtd = g.Sum(x => x.Quantidade);
            var preco = qtd > 0 ? Math.Round(g.Sum(x => x.Total) / qtd, 4) : g.First().PrecoUnitario;
            novo.AdicionarItem(g.Key, g.First().Descricao, qtd, preco);
        }
        novo.DefinirObservacao($"Unido dos pedidos: {string.Join(", ", pedidos.Select(p => p.Numero).OrderBy(n => n))}");
        // Se todos os originais já estavam Enviados, o unido nasce Enviado (mantém o "a caminho").
        if (pedidos.All(p => p.Status == StatusPedidoCompra.Enviado))
            novo.Enviar();
        db.PedidosCompra.Add(novo);

        // Remove os originais (rascunho, sem escrituração) e seus itens.
        var itensOriginais = await db.ItensPedidoCompra.Where(i => ids.Contains(i.PedidoCompraId)).ToListAsync(ct);
        db.ItensPedidoCompra.RemoveRange(itensOriginais);
        db.PedidosCompra.RemoveRange(pedidos);

        await uow.SalvarAsync(ct);
        return Ok(new
        {
            id = novo.Id, numero,
            produtos = novo.Itens.Count,
            unidos = pedidos.Count,
            mensagem = $"{pedidos.Count} pedidos unidos no pedido {numero} ({novo.Itens.Count} produtos).",
        });
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var pedidos = (await repo.ListarPorPeriodoAsync(empresaId, inicio, fim, ct)).ToList();

        if (!string.IsNullOrEmpty(status) && status != "Todos"
            && Enum.TryParse<StatusPedidoCompra>(status, out var st))
            pedidos = pedidos.Where(p => p.Status == st).ToList();

        var fornecedorIds = pedidos.Where(p => p.FornecedorId.HasValue).Select(p => p.FornecedorId!.Value).Distinct().ToList();
        var nomes = fornecedorIds.Count > 0
            ? await db.Fornecedores.AsNoTracking()
                .Where(f => fornecedorIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.RazaoSocial, ct)
            : new Dictionary<Guid, string>();

        var lojaIds = pedidos.Where(p => p.LocalEstoqueId.HasValue).Select(p => p.LocalEstoqueId!.Value).Distinct().ToList();
        var lojas = lojaIds.Count > 0
            ? await db.LocaisEstoque.AsNoTracking().Where(l => lojaIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Nome, ct)
            : new Dictionary<Guid, string>();

        return Ok(pedidos.Select(p => new
        {
            p.Id, p.Numero, p.FornecedorId,
            fornecedorNome = p.FornecedorId.HasValue ? nomes.GetValueOrDefault(p.FornecedorId.Value, "—") : null,
            p.LocalEstoqueId,
            lojaNome = p.LocalEstoqueId.HasValue ? lojas.GetValueOrDefault(p.LocalEstoqueId.Value, "—") : null,
            status = p.Status.ToString(),
            criadoEm = p.DataPedido, p.DataPedido, p.DataPrevisaoEntrega, p.DataRecebimento,
            p.NotaFiscalRecebimento,
            totalPedido = p.Total, QtdItens = p.Itens.Count
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null) return NotFound();
        var lojaNome = pedido.LocalEstoqueId.HasValue
            ? await db.LocaisEstoque.AsNoTracking().Where(l => l.Id == pedido.LocalEstoqueId).Select(l => l.Nome).FirstOrDefaultAsync(ct)
            : null;
        return Ok(new
        {
            pedido.Id, pedido.Numero, pedido.FornecedorId, pedido.Status,
            pedido.LocalEstoqueId, lojaNome,
            pedido.DataPedido, pedido.DataPrevisaoEntrega, pedido.DataRecebimento,
            pedido.NotaFiscalRecebimento, pedido.Total, pedido.AnexoUrl,
            Itens = pedido.Itens.Select(i => new
            {
                i.Id, i.ProdutoId, i.Descricao,
                i.Quantidade, i.PrecoUnitario, i.Total
            })
        });
    }

    /// <summary>Anexa o PDF do fornecedor (resposta/disponibilidade) ao pedido.</summary>
    [HttpPost("{id:guid}/anexo")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Anexo(Guid id, IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0) return BadRequest("Nenhum arquivo enviado.");
        if (!arquivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !arquivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Apenas arquivos PDF são aceitos.");

        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null) return NotFound();

        var dir = Path.Combine("wwwroot", "uploads", "pedidos-compra");
        Directory.CreateDirectory(dir);
        var caminho = Path.Combine(dir, $"{id}.pdf");
        using (var stream = System.IO.File.Create(caminho))
            await arquivo.CopyToAsync(stream, ct);

        var url = $"/uploads/pedidos-compra/{id}.pdf";
        pedido.DefinirAnexo(url);
        repo.Atualizar(pedido);
        await uow.SalvarAsync(ct);

        // Extrai o texto do PDF e compara por semelhança com os itens do pedido:
        // marca quais o fornecedor TEM (encontrado) e quais FALTAM.
        var comparacao = CompararItensComPdf(caminho, pedido.Itens);
        return Ok(new { url, comparacao });
    }

    private static List<object> CompararItensComPdf(string caminho, IReadOnlyList<ItemPedidoCompra> itens)
    {
        var pdfTokens = new HashSet<string>();
        try
        {
            using var pdf = PdfDocument.Open(caminho);
            var sb = new StringBuilder();
            foreach (var page in pdf.GetPages()) sb.Append(' ').Append(page.Text);
            foreach (var t in Tokens(sb.ToString())) pdfTokens.Add(t);
        }
        catch { /* PDF ilegível (imagem/escaneado) → retorna tudo como não encontrado */ }

        var resultado = new List<object>();
        foreach (var i in itens)
        {
            var tokens = Tokens(i.Descricao).Distinct().ToList();
            var achados = tokens.Count(t => pdfTokens.Contains(t));
            var cobertura = tokens.Count > 0 ? (double)achados / tokens.Count : 0;
            resultado.Add(new
            {
                itemId = i.Id,
                encontrado = pdfTokens.Count > 0 && cobertura >= 0.6,   // fornecedor TEM o item
                cobertura = Math.Round(cobertura * 100, 0)
            });
        }
        return resultado;
    }

    private static readonly HashSet<string> Stop = ["DE", "DA", "DO", "EM", "COM", "SEM", "PARA", "PO", "KG", "UN", "G", "ML"];
    private static IEnumerable<string> Tokens(string s)
    {
        var formD = (s ?? "").ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in formD)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        var limpo = Regex.Replace(sb.ToString(), "[^A-Z0-9]+", " ");
        return limpo.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !Stop.Contains(w));
    }

    /// <summary>Remove itens do pedido (ex.: faltantes que foram para outro fornecedor).</summary>
    [HttpPost("{id:guid}/remover-itens")]
    public async Task<IActionResult> RemoverItens(Guid id, [FromBody] RemoverItensRequest req, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null) return NotFound();
        pedido.RemoverItens(req.ItemIds ?? []);
        repo.Atualizar(pedido);
        await uow.SalvarAsync(ct);
        return Ok(new { total = pedido.Total, itensRestantes = pedido.Itens.Count });
    }
}

public record ReceberRequest(Guid LocalEstoqueId, Guid UsuarioId, List<ReceberItemRequest>? Itens = null);
public record ReceberItemRequest(Guid ProdutoId, decimal Quantidade);
public record DefinirLojaRequest(Guid? LocalEstoqueId);
public record DefinirFornecedorRequest(Guid? FornecedorId);
public record RemoverItensRequest(List<Guid> ItemIds);
public record UnirPedidosRequest(Guid EmpresaId, Guid UsuarioId, List<Guid> PedidoIds);
