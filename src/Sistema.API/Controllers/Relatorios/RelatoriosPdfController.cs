using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

/// <summary>Geração de relatórios em PDF via QuestPDF.</summary>
[ApiController]
[Route("api/relatorios/pdf")]
[Authorize]
public class RelatoriosPdfController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Relatório PDF de vendas do período.</summary>
    [HttpGet("vendas")]
    public async Task<IActionResult> VendasPdf([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empresaId, ct);

        var vendas = await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora < fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .OrderBy(v => v.DataHora)
            .Select(v => new
            {
                v.Numero, v.DataHora, v.Total, v.TotalDesconto,
                clienteId = v.ClienteId
            })
            .ToListAsync(ct);

        var totalGeral = vendas.Sum(v => v.Total);
        var totalDescontos = vendas.Sum(v => v.TotalDesconto);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(15, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(empresa?.RazaoSocial ?? "Empresa").Bold().FontSize(14);
                            c.Item().Text($"CNPJ: {empresa?.Cnpj ?? ""}").FontSize(8);
                        });
                        row.ConstantItem(150).AlignRight().Column(c =>
                        {
                            c.Item().Text("RELATÓRIO DE VENDAS").Bold().FontSize(12);
                            c.Item().Text($"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}").FontSize(9);
                            c.Item().Text($"Emitido: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8);
                        });
                    });
                    h.Item().PaddingTop(4).LineHorizontal(1);
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    // Resumo
                    col.Item().Background(Colors.Grey.Lighten3).Padding(6).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Total de Vendas: {vendas.Count}").Bold();
                            c.Item().Text($"Descontos: R$ {totalDescontos:F2}");
                        });
                        row.ConstantItem(150).AlignRight().Column(c =>
                        {
                            c.Item().Text("FATURAMENTO TOTAL").FontSize(8);
                            c.Item().Text($"R$ {totalGeral:F2}")
                                .Bold().FontSize(16).FontColor(Colors.Green.Darken3);
                        });
                    });

                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(60); // Nº
                            cols.RelativeColumn();    // Data/Hora
                            cols.RelativeColumn();    // Desconto
                            cols.RelativeColumn();    // Total
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Darken2)
                                .Padding(4).Text("Nº Venda").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Grey.Darken2)
                                .Padding(4).Text("Data/Hora").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Grey.Darken2)
                                .Padding(4).Text("Desconto").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Grey.Darken2)
                                .Padding(4).Text("Total").FontColor(Colors.White).Bold();
                        });

                        var linhaImpar = true;
                        foreach (var v in vendas)
                        {
                            var bg = linhaImpar ? Colors.White : Colors.Grey.Lighten5;
                            table.Cell().Background(bg).Padding(3).Text(v.Numero.ToString());
                            table.Cell().Background(bg).Padding(3).Text(v.DataHora.ToString("dd/MM/yy HH:mm"));
                            table.Cell().Background(bg).Padding(3).Text($"R$ {v.TotalDesconto:F2}");
                            table.Cell().Background(bg).Padding(3).AlignRight().Text($"R$ {v.Total:F2}").Bold();
                            linhaImpar = !linhaImpar;
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Página ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" de ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        return File(bytes, "application/pdf",
            $"vendas_{inicio:yyyyMMdd}_{fim:yyyyMMdd}.pdf");
    }

    [HttpGet("posicao-estoque")]
    public async Task<IActionResult> PosicaoEstoquePdf([FromQuery] Guid empresaId,
        [FromQuery] bool apenasAbaixoMinimo = false, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        var query = db.Produtos.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Ativo);
        if (apenasAbaixoMinimo) query = query.Where(p => p.EstoqueAtual <= p.EstoqueMinimo);
        var produtos = await query
            .OrderBy(p => p.Descricao)
            .Select(p => new { p.Codigo, p.Descricao, p.EstoqueAtual, p.EstoqueMinimo, p.CustoUnitario, p.PrecoVenda })
            .ToListAsync(ct);

        var totalCusto = produtos.Sum(p => p.EstoqueAtual * p.CustoUnitario);
        var totalVenda = produtos.Sum(p => p.EstoqueAtual * p.PrecoVenda);
        var abaixoMinimo = produtos.Count(p => p.EstoqueAtual <= p.EstoqueMinimo);

        var pdf = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(15, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(9));
            page.Header().Column(h =>
            {
                h.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(empresa?.RazaoSocial ?? "").Bold().FontSize(13);
                        c.Item().Text($"CNPJ: {empresa?.Cnpj ?? ""}").FontSize(8);
                    });
                    row.ConstantItem(180).AlignRight().Column(c =>
                    {
                        c.Item().Text("POSIÇÃO DE ESTOQUE").Bold().FontSize(12);
                        c.Item().Text($"Emitido: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8);
                        if (apenasAbaixoMinimo)
                            c.Item().Text("Apenas abaixo do mínimo").FontColor(Colors.Red.Medium).FontSize(8).Bold();
                    });
                });
                h.Item().PaddingTop(4).LineHorizontal(1);
            });
            page.Content().PaddingTop(6).Column(col =>
            {
                col.Item().Background(Colors.Grey.Lighten3).Padding(6).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"{produtos.Count} produtos | {abaixoMinimo} abaixo do mínimo").Bold();
                    });
                    row.ConstantItem(200).AlignRight().Column(c =>
                    {
                        c.Item().Text($"Custo total: R$ {totalCusto:F2}").Bold();
                        c.Item().Text($"Val. venda: R$ {totalVenda:F2}").FontSize(8);
                    });
                });
                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55); cols.RelativeColumn(); cols.ConstantColumn(55);
                        cols.ConstantColumn(55); cols.ConstantColumn(75); cols.ConstantColumn(75);
                    });
                    void Hdr(string t) => table.Cell().Background(Colors.Grey.Darken2).Padding(3).Text(t).FontColor(Colors.White).Bold();
                    Hdr("Código"); Hdr("Descrição"); Hdr("Estoque"); Hdr("Mínimo"); Hdr("Custo Unit."); Hdr("Preço Venda");
                    var impar = true;
                    foreach (var p in produtos)
                    {
                        var bg = p.EstoqueAtual <= p.EstoqueMinimo ? Colors.Red.Lighten4
                               : impar ? Colors.White : Colors.Grey.Lighten5;
                        table.Cell().Background(bg).Padding(3).Text(p.Codigo);
                        table.Cell().Background(bg).Padding(3).Text(p.Descricao);
                        table.Cell().Background(bg).Padding(3).AlignRight().Text(p.EstoqueAtual.ToString("F2"));
                        table.Cell().Background(bg).Padding(3).AlignRight().Text(p.EstoqueMinimo.ToString("F2"));
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"R$ {p.CustoUnitario:F2}");
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"R$ {p.PrecoVenda:F2}").Bold();
                        impar = !impar;
                    }
                });
            });
            page.Footer().AlignCenter().Text(t => { t.Span("Página ").FontSize(8); t.CurrentPageNumber().FontSize(8); t.Span(" de ").FontSize(8); t.TotalPages().FontSize(8); });
        }));
        return File(pdf.GeneratePdf(), "application/pdf", $"posicao_estoque_{DateTime.Now:yyyyMMdd}.pdf");
    }

    [HttpGet("curva-abc-produtos")]
    public async Task<IActionResult> CurvaAbcPdf([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        var itens = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas.Where(v => v.EmpresaId == empresaId
                && v.DataHora >= inicio && v.DataHora < fim.AddDays(1)
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada),
                i => i.VendaId, v => v.Id, (i, v) => i)
            .GroupBy(i => new { i.ProdutoId, i.Descricao })
            .Select(g => new { g.Key.Descricao, Total = g.Sum(i => i.Total), Qtd = g.Sum(i => i.Quantidade) })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        var totalGeral = itens.Sum(i => i.Total);
        decimal acumulado = 0;
        var comCurva = itens.Select((i, idx) =>
        {
            acumulado += i.Total;
            var pct = totalGeral > 0 ? acumulado / totalGeral * 100 : 0;
            var curva = pct <= 80 ? "A" : pct <= 95 ? "B" : "C";
            return new { i.Descricao, i.Total, i.Qtd, PctAcumulado = pct, Curva = curva, Pos = idx + 1 };
        }).ToList();

        var qtdA = comCurva.Count(x => x.Curva == "A");
        var qtdB = comCurva.Count(x => x.Curva == "B");
        var qtdC = comCurva.Count(x => x.Curva == "C");

        var pdf = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(15, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(9));
            page.Header().Column(h =>
            {
                h.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(empresa?.RazaoSocial ?? "").Bold().FontSize(13);
                    });
                    row.ConstantItem(180).AlignRight().Column(c =>
                    {
                        c.Item().Text("CURVA ABC DE PRODUTOS").Bold().FontSize(12);
                        c.Item().Text($"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}").FontSize(9);
                    });
                });
                h.Item().PaddingTop(4).LineHorizontal(1);
            });
            page.Content().PaddingTop(6).Column(col =>
            {
                col.Item().Background(Colors.Grey.Lighten3).Padding(5).Row(row =>
                {
                    row.RelativeItem().Text($"{itens.Count} produtos | Total: R$ {totalGeral:F2}").Bold();
                    row.ConstantItem(200).AlignRight().Column(c =>
                    {
                        c.Item().Text($"A={qtdA}  B={qtdB}  C={qtdC}").FontSize(8);
                    });
                });
                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(35); cols.RelativeColumn(); cols.ConstantColumn(55);
                        cols.ConstantColumn(85); cols.ConstantColumn(70); cols.ConstantColumn(35);
                    });
                    void Hdr(string t) => table.Cell().Background(Colors.Grey.Darken2).Padding(3).Text(t).FontColor(Colors.White).Bold();
                    Hdr("#"); Hdr("Produto"); Hdr("Qtd Vend."); Hdr("Faturado"); Hdr("% Acum."); Hdr("Curva");
                    var impar = true;
                    foreach (var p in comCurva)
                    {
                        var bg = p.Curva == "A" ? Colors.Green.Lighten4 : p.Curva == "B" ? Colors.Yellow.Lighten4 : Colors.Red.Lighten4;
                        table.Cell().Background(bg).Padding(3).Text(p.Pos.ToString());
                        table.Cell().Background(bg).Padding(3).Text(p.Descricao);
                        table.Cell().Background(bg).Padding(3).AlignRight().Text(p.Qtd.ToString("F1"));
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"R$ {p.Total:F2}").Bold();
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"{p.PctAcumulado:F1}%");
                        table.Cell().Background(bg).Padding(3).AlignCenter().Text(p.Curva).Bold();
                        impar = !impar;
                    }
                });
            });
            page.Footer().AlignCenter().Text(t => { t.Span("Página ").FontSize(8); t.CurrentPageNumber().FontSize(8); t.Span(" de ").FontSize(8); t.TotalPages().FontSize(8); });
        }));
        return File(pdf.GeneratePdf(), "application/pdf", $"curva_abc_{inicio:yyyyMMdd}_{fim:yyyyMMdd}.pdf");
    }

    [HttpGet("contas-receber")]
    public async Task<IActionResult> ContasReceberPdf([FromQuery] Guid empresaId,
        [FromQuery] DateTime? vencimentoAte = null, [FromQuery] bool apenasVencidas = false,
        CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        var query = db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == Domain.Financeiro.Entities.TipoLancamento.ContaReceber
                && l.Status != Domain.Financeiro.Entities.StatusLancamento.Pago);
        if (apenasVencidas) query = query.Where(l => l.DataVencimento < DateTime.Today);
        if (vencimentoAte.HasValue) query = query.Where(l => l.DataVencimento <= vencimentoAte.Value);
        var lancamentos = await query.OrderBy(l => l.DataVencimento)
            .Select(l => new { l.Descricao, l.DataVencimento, Valor = l.ValorOriginal, l.Status })
            .ToListAsync(ct);

        var total = lancamentos.Sum(l => l.Valor);
        var vencidos = lancamentos.Where(l => l.DataVencimento < DateTime.Today).Sum(l => l.Valor);

        var pdf = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(15, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(9));
            page.Header().Column(h =>
            {
                h.Item().Row(row =>
                {
                    row.RelativeItem().Column(c => c.Item().Text(empresa?.RazaoSocial ?? "").Bold().FontSize(13));
                    row.ConstantItem(180).AlignRight().Column(c =>
                    {
                        c.Item().Text("CONTAS A RECEBER").Bold().FontSize(12);
                        c.Item().Text($"Emitido: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8);
                    });
                });
                h.Item().PaddingTop(4).LineHorizontal(1);
            });
            page.Content().PaddingTop(6).Column(col =>
            {
                col.Item().Background(Colors.Grey.Lighten3).Padding(6).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"{lancamentos.Count} lançamentos | Vencidos: R$ {vencidos:F2}").Bold();
                    });
                    row.ConstantItem(180).AlignRight().Column(c =>
                    {
                        c.Item().Text("TOTAL A RECEBER").FontSize(8);
                        c.Item().Text($"R$ {total:F2}").Bold().FontSize(14).FontColor(Colors.Green.Darken3);
                    });
                });
                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); cols.RelativeColumn(); cols.ConstantColumn(90); cols.ConstantColumn(60);
                    });
                    void Hdr(string t) => table.Cell().Background(Colors.Grey.Darken2).Padding(3).Text(t).FontColor(Colors.White).Bold();
                    Hdr("Descrição"); Hdr("Valor"); Hdr("Vencimento"); Hdr("Status");
                    var impar = true;
                    foreach (var l in lancamentos)
                    {
                        var vencido = l.DataVencimento < DateTime.Today;
                        var bg = vencido ? Colors.Red.Lighten4 : impar ? Colors.White : Colors.Grey.Lighten5;
                        table.Cell().Background(bg).Padding(3).Text(l.Descricao);
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"R$ {l.Valor:F2}").Bold();
                        table.Cell().Background(bg).Padding(3).AlignCenter().Text(l.DataVencimento.ToString("dd/MM/yyyy"));
                        table.Cell().Background(bg).Padding(3).AlignCenter().Text(vencido ? "VENCIDO" : "A vencer")
                            .FontColor(vencido ? Colors.Red.Darken2 : Colors.Grey.Darken1);
                        impar = !impar;
                    }
                });
            });
            page.Footer().AlignCenter().Text(t => { t.Span("Página ").FontSize(8); t.CurrentPageNumber().FontSize(8); t.Span(" de ").FontSize(8); t.TotalPages().FontSize(8); });
        }));
        return File(pdf.GeneratePdf(), "application/pdf", $"contas_receber_{DateTime.Now:yyyyMMdd}.pdf");
    }
}
