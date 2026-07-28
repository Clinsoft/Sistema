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
        await mediator.Send(new ReceberPedidoCompraCommand(id, req.LocalEstoqueId, req.UsuarioId), ct);
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

        var fornecedorIds = pedidos.Select(p => p.FornecedorId).Distinct().ToList();
        var nomes = fornecedorIds.Count > 0
            ? await db.Fornecedores.AsNoTracking()
                .Where(f => fornecedorIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.RazaoSocial, ct)
            : new Dictionary<Guid, string>();

        return Ok(pedidos.Select(p => new
        {
            p.Id, p.Numero, p.FornecedorId,
            fornecedorNome = nomes.GetValueOrDefault(p.FornecedorId, "—"),
            status = p.Status.ToString(),
            criadoEm = p.DataPedido, p.DataPedido, p.DataPrevisaoEntrega, p.DataRecebimento,
            totalPedido = p.Total, QtdItens = p.Itens.Count
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var pedido = await repo.ObterComItensAsync(id, ct);
        if (pedido is null) return NotFound();
        return Ok(new
        {
            pedido.Id, pedido.Numero, pedido.FornecedorId, pedido.Status,
            pedido.DataPedido, pedido.DataPrevisaoEntrega, pedido.Total, pedido.AnexoUrl,
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

public record ReceberRequest(Guid LocalEstoqueId, Guid UsuarioId);
public record RemoverItensRequest(List<Guid> ItemIds);
