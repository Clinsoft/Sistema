using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QUnit = QuestPDF.Infrastructure.Unit;
using Sistema.Application.Crediario.Commands;
using Sistema.Domain.Crediario.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Crediario;

[ApiController]
[Route("api/crediario")]
[Authorize]
public class CrediarioController(IMediator mediator, ICrediarioRepository repo, SistemaDbContext db) : ControllerBase
{
    /// <summary>Abre um novo crediário para o cliente.</summary>
    [HttpPost]
    public async Task<IActionResult> Abrir([FromBody] AbrirCrediarioCommand cmd, CancellationToken ct)
    {
        var resultado = await mediator.Send(cmd, ct);
        return Ok(resultado);
    }

    /// <summary>Lista todos os crediários da empresa (para tela de gestão).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] string? q, CancellationToken ct)
    {
        var crediarios = await db.Crediarios.AsNoTracking()
            .Include(c => c.Parcelas)
            .Where(c => c.EmpresaId == empresaId)
            .Join(db.Clientes, c => c.ClienteId, cl => cl.Id, (c, cl) => new { c, clienteNome = cl.Nome })
            .Where(x => q == null || x.clienteNome.Contains(q))
            .OrderByDescending(x => x.c.DataContrato)
            .ToListAsync(ct);

        return Ok(crediarios.Select(x => new
        {
            x.c.Id, x.c.Numero, x.c.ValorTotal, x.c.ValorFinanciado,
            x.c.NumeroParcelas, x.c.Status, x.c.DataContrato,
            x.clienteNome,
            limiteCredito = 0m,
            saldoDevedor = x.c.SaldoDevedor(),
            inadimplente = x.c.Inadimplente()
        }));
    }

    /// <summary>Retorna as parcelas de um crediário específico.</summary>
    [HttpGet("{id:guid}/parcelas")]
    public async Task<IActionResult> Parcelas(Guid id, CancellationToken ct)
    {
        var crediario = await db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Crediário não encontrado.");

        return Ok(crediario.Parcelas.OrderBy(p => p.Numero).Select(p => new
        {
            p.Id, p.Numero, p.Valor, p.DataVencimento,
            p.DataPagamento, p.ValorPago, p.Status,
            ValorAtualizado = p.ValorAtualizado()
        }));
    }

    /// <summary>Gera o carnê PDF do crediário via QuestPDF.</summary>
    [HttpGet("{id:guid}/carne")]
    public async Task<IActionResult> Carne(Guid id, CancellationToken ct)
    {
        var crediario = await db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Crediário não encontrado.");

        var cliente = await db.Clientes.FindAsync([crediario.ClienteId], ct);

        var pdf = Document.Create(container =>
        {
            foreach (var parcela in crediario.Parcelas.OrderBy(p => p.Numero))
            {
                container.Page(page =>
                {
                    page.Size(14.8f, 10.5f, QUnit.Centimetre);
                    page.Margin(0.8f, QUnit.Centimetre);
                    page.Content().Column(col =>
                    {
                        col.Item().Text($"CARNÊ DE PAGAMENTO — Parcela {parcela.Numero}/{crediario.NumeroParcelas}")
                            .Bold().FontSize(10);
                        col.Item().Text($"Cliente: {cliente?.Nome ?? "-"}").FontSize(9);
                        col.Item().Text($"Vencimento: {parcela.DataVencimento:dd/MM/yyyy}").FontSize(9);
                        col.Item().Text($"Valor: R$ {parcela.Valor:F2}").Bold().FontSize(12);
                        col.Item().PaddingTop(4).Text($"Status: {parcela.Status}").FontSize(8);
                        col.Item().LineHorizontal(0.5f);
                        col.Item().Text("Loja Clinsoft — Produtos Naturais").FontSize(7).Italic();
                    });
                });
            }
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"Carne_{crediario.Numero}.pdf");
    }

    /// <summary>Lista crediários de um cliente.</summary>
    [HttpGet("cliente/{clienteId:guid}")]
    public async Task<IActionResult> ListarPorCliente(Guid clienteId, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var crediarios = await repo.ListarPorClienteAsync(empresaId, clienteId, ct);

        return Ok(crediarios.Select(c => new
        {
            c.Id, c.Numero, c.ValorTotal, c.ValorFinanciado,
            c.NumeroParcelas, c.TaxaJurosMensal,
            c.Status, c.DataContrato,
            SaldoDevedor = c.SaldoDevedor(),
            Inadimplente = c.Inadimplente(),
            Parcelas = c.Parcelas.Select(p => new
            {
                p.Id, p.Numero, p.Valor, p.DataVencimento,
                p.DataPagamento, p.ValorPago, p.Status,
                ValorAtualizado = p.ValorAtualizado()
            })
        }));
    }

    /// <summary>Registra o pagamento de uma parcela.</summary>
    [HttpPost("parcelas/{parcelaId:guid}/pagar")]
    public async Task<IActionResult> PagarParcela(Guid parcelaId, [FromBody] PagarParcelaRequest req, CancellationToken ct)
    {
        var resultado = await mediator.Send(new PagarParcelaCommand(parcelaId, req.ValorPago), ct);
        return Ok(resultado);
    }
}

public record PagarParcelaRequest(decimal ValorPago);
