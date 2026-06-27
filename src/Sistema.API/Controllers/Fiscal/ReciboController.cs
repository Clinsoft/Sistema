using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/recibo")]
[Authorize]
public class ReciboController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Gera recibo PDF de pagamento de conta a receber.</summary>
    [HttpGet("lancamento/{id:guid}")]
    public async Task<IActionResult> ReciboPagamento(Guid id, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var lancamento = await db.LancamentosFinanceiros.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == lancamento.EmpresaId, ct);

        string? clienteNome = null;
        if (lancamento.ClienteId.HasValue)
            clienteNome = (await db.Clientes.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == lancamento.ClienteId, ct))?.Nome;

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(12, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text("RECIBO DE PAGAMENTO")
                        .Bold().FontSize(14);
                    col.Item().PaddingTop(4).LineHorizontal(1);

                    col.Item().PaddingTop(8).Column(c =>
                    {
                        c.Item().Text(empresa?.RazaoSocial ?? "Empresa").Bold();
                        c.Item().Text($"CNPJ: {empresa?.Cnpj ?? ""}").FontSize(8);
                        c.Item().PaddingTop(4).Text($"Recebemos de: {clienteNome ?? "—"}").Bold();
                        c.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("Valor:");
                            row.ConstantItem(120).AlignRight()
                                .Text($"R$ {lancamento.ValorPago:F2}").Bold().FontSize(14);
                        });
                        c.Item().PaddingTop(4).Text(
                            $"({lancamento.ValorPago.ToString("C")} — por extenso)").FontSize(8);
                        c.Item().PaddingTop(6).Text($"Referente: {lancamento.Descricao}");
                        if (!string.IsNullOrEmpty(lancamento.DocumentoOrigem))
                            c.Item().Text($"Documento: {lancamento.DocumentoOrigem}").FontSize(8);
                        c.Item().PaddingTop(4).Text(
                            $"Pago em: {lancamento.DataPagamento:dd/MM/yyyy}").Bold();
                    });

                    col.Item().PaddingTop(16).LineHorizontal(0.5f);
                    col.Item().PaddingTop(8).AlignCenter()
                        .Text($"Emitido em: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8);
                });
            });
        });

        return File(pdf.GeneratePdf(), "application/pdf",
            $"recibo-{lancamento.Id:N}.pdf");
    }

    /// <summary>Gera recibo PDF de venda (comprovante simples).</summary>
    [HttpGet("venda/{vendaId:guid}")]
    public async Task<IActionResult> ReciboVenda(Guid vendaId, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var venda = await db.Vendas.AsNoTracking()
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .FirstOrDefaultAsync(v => v.Id == vendaId, ct)
            ?? throw new KeyNotFoundException("Venda não encontrada.");

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == venda.EmpresaId, ct);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(80, 300, Unit.Millimetre); // Bobina térmica 80mm
                page.Margin(4, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(8));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text(empresa?.RazaoSocial ?? "LOJA").Bold().FontSize(10);
                    col.Item().AlignCenter().Text($"CNPJ: {empresa?.Cnpj ?? ""}");
                    col.Item().AlignCenter().Text("------- COMPROVANTE -------");
                    col.Item().Text($"Venda Nº: {venda.Numero}");
                    col.Item().Text($"Data: {venda.DataHora:dd/MM/yyyy HH:mm}");
                    col.Item().Text("--------------------------------");

                    foreach (var item in venda.Itens)
                    {
                        col.Item().Text($"{item.Descricao}");
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"  {item.Quantidade}x R$ {item.PrecoUnitario:F2}");
                            row.ConstantItem(60).AlignRight().Text($"R$ {item.Total:F2}");
                        });
                    }

                    col.Item().Text("--------------------------------");
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL").Bold();
                        row.ConstantItem(60).AlignRight().Text($"R$ {venda.Total:F2}").Bold();
                    });

                    foreach (var pag in venda.Pagamentos)
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(pag.Forma.ToString());
                            row.ConstantItem(60).AlignRight().Text($"R$ {pag.Valor:F2}");
                        });

                    if (venda.Troco > 0)
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("TROCO");
                            row.ConstantItem(60).AlignRight().Text($"R$ {venda.Troco:F2}");
                        });

                    col.Item().PaddingTop(4).AlignCenter().Text("Obrigado pela preferência!").Italic();
                });
            });
        });

        return File(pdf.GeneratePdf(), "application/pdf", $"recibo-venda-{venda.Numero}.pdf");
    }
}
