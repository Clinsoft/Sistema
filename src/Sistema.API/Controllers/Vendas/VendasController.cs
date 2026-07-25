using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Vendas.Commands;
using Sistema.Application.Vendas.Queries;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Vendas;

[ApiController]
[Route("api/vendas")]
[Authorize]
public class VendasController(IMediator mediator, SistemaDbContext db) : ControllerBase
{
    /// <summary>Inicia uma nova venda no PDV.</summary>
    [HttpPost("iniciar")]
    public async Task<IActionResult> Iniciar([FromBody] IniciarVendaCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Ok(new { id });
    }

    /// <summary>Adiciona um item à venda aberta.</summary>
    [HttpPost("{id:guid}/itens")]
    public async Task<IActionResult> AdicionarItem(Guid id, [FromBody] AdicionarItemRequest req, CancellationToken ct)
    {
        await mediator.Send(new AdicionarItemVendaCommand(id, req.ProdutoId, req.Quantidade, req.PrecoUnitario, req.PercentualDesconto), ct);
        return NoContent();
    }

    /// <summary>
    /// Finaliza a venda e emite a NFC-e automaticamente.
    /// O QR Code retornado deve ser exibido na tela do PDV para o consumidor consultar.
    /// </summary>
    [HttpPost("{id:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(Guid id, [FromBody] FinalizarVendaRequest req, CancellationToken ct)
    {
        var resultado = await mediator.Send(
            new FinalizarVendaCommand(id, req.Pagamentos, req.CpfCnpjConsumidor), ct);

        // Após o MediatR publicar o VendaFinalizadaEvent, o EmitirNFCeHandler já salvou a NFC-e
        // (despacho síncrono no SaveChanges). Buscamos os dados da nota para retornar ao PDV.
        string? qrCode = null;
        string? chaveAcesso = null;
        bool nfceAutorizada = false;
        bool imprimirAutomatico = false;
        if (resultado.NotaFiscalId.HasValue)
        {
            var nota = await db.NotasFiscais.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == resultado.NotaFiscalId.Value, ct);
            if (nota is not null)
            {
                nfceAutorizada = nota.Status == StatusNF.Autorizada;
                // QR/chave só fazem sentido quando a nota foi autorizada.
                qrCode = nfceAutorizada ? nota.QrCode : null;
                chaveAcesso = nfceAutorizada ? nota.ChaveAcesso : null;

                var cfg = await db.ConfiguracoesFiscais.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.EmpresaId == nota.EmpresaId, ct);
                imprimirAutomatico = cfg?.ImprimirAutomaticamenteNFCe ?? false;
            }
        }

        return Ok(new
        {
            resultado.Numero, resultado.Total, resultado.Troco, resultado.NotaFiscalId,
            qrCode, chaveAcesso, nfceAutorizada, imprimirAutomatico,
        });
    }

    /// <summary>Lista vendas por período, com nome do cliente e filtro opcional por status.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        IEnumerable<VendaResumoDto> resultado =
            await mediator.Send(new ListarVendasQuery(empresaId, inicio, fim), ct);

        if (!string.IsNullOrEmpty(status))
            resultado = resultado.Where(v => v.Status == status);

        var lista = resultado.ToList();
        var clienteIds = lista.Where(v => v.ClienteId.HasValue)
            .Select(v => v.ClienteId!.Value).Distinct().ToList();
        var nomes = clienteIds.Count > 0
            ? await db.Clientes.AsNoTracking()
                .Where(c => clienteIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Nome, ct)
            : new Dictionary<Guid, string>();

        return Ok(lista.Select(v => new
        {
            v.Id, v.Numero, v.DataHora, v.Status, v.ClienteId,
            clienteNome = v.ClienteId.HasValue && nomes.TryGetValue(v.ClienteId.Value, out var n) ? n : null,
            v.Total, v.TotalPago, v.Troco, v.QtdItens
        }));
    }

    /// <summary>Detalhe completo de uma venda: itens, pagamentos e totais.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalhe(Guid id, CancellationToken ct)
    {
        var venda = await db.Vendas.AsNoTracking()
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (venda is null) return NotFound(new { mensagem = "Venda não encontrada." });

        string? clienteNome = null;
        if (venda.ClienteId.HasValue)
            clienteNome = await db.Clientes.AsNoTracking()
                .Where(c => c.Id == venda.ClienteId.Value)
                .Select(c => c.Nome).FirstOrDefaultAsync(ct);

        return Ok(new
        {
            venda.Id, venda.Numero, status = venda.Status.ToString(),
            venda.DataHora, venda.SubTotal, venda.TotalDesconto, venda.TotalAcrescimo,
            venda.Total, venda.TotalPago, venda.Troco, venda.Observacao,
            venda.CpfCnpjConsumidor, venda.ClienteId, clienteNome,
            itens = venda.Itens.Select(i => new
            {
                i.Id, i.ProdutoId, i.Descricao, i.Quantidade,
                i.PrecoUnitario, i.PercentualDesconto, i.TotalDesconto, i.Total
            }),
            pagamentos = venda.Pagamentos.Select(p => new
            {
                p.Id, forma = FormaLabel(p.Forma), p.Valor, p.Parcelas
            })
        });
    }

    private static string FormaLabel(Domain.Vendas.Entities.FormaPagamento f) => f switch
    {
        Domain.Vendas.Entities.FormaPagamento.CartaoCredito => "Crédito",
        Domain.Vendas.Entities.FormaPagamento.CartaoDebito => "Débito",
        Domain.Vendas.Entities.FormaPagamento.Crediario => "Crediário",
        _ => f.ToString()
    };
}

public record AdicionarItemRequest(Guid ProdutoId, decimal Quantidade, decimal? PrecoUnitario = null, decimal PercentualDesconto = 0);
public record FinalizarVendaRequest(IList<PagamentoDto> Pagamentos, string? CpfCnpjConsumidor = null);
