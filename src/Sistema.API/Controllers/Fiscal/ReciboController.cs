using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/recibo")]
[Authorize]
public class ReciboController(SistemaDbContext db, IDanfeService danfe) : ControllerBase
{
    /// <summary>Reimprime o cupom fiscal (DANFE da NFC-e) de uma venda que teve NFC-e autorizada.</summary>
    [HttpGet("venda/{vendaId:guid}/nfce")]
    public async Task<IActionResult> ReciboFiscalVenda(Guid vendaId, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var nota = await db.NotasFiscais.AsNoTracking()
            .Include(n => n.Itens)
            .Where(n => n.VendaId == vendaId && n.Modelo == ModeloNF.NFCe && n.Status == StatusNF.Autorizada)
            .OrderByDescending(n => n.DataEmissao)
            .FirstOrDefaultAsync(ct);

        if (nota is null)
            return NotFound(new { mensagem = "Esta venda não teve NFC-e autorizada — só é possível reimprimir o cupom simples." });

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == nota.EmpresaId, ct);
        if (empresa is null)
            return NotFound(new { mensagem = "Empresa não encontrada." });

        var pdf = danfe.GerarDanfe(nota, empresa);
        return File(pdf, "application/pdf", $"nfce-{nota.Numero:D9}.pdf");
    }

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

        static string FormaTexto(Domain.Vendas.Entities.FormaPagamento f) => f switch
        {
            Domain.Vendas.Entities.FormaPagamento.Dinheiro      => "Dinheiro",
            Domain.Vendas.Entities.FormaPagamento.Pix           => "Pix",
            Domain.Vendas.Entities.FormaPagamento.CartaoCredito => "Cartao Credito",
            Domain.Vendas.Entities.FormaPagamento.CartaoDebito  => "Cartao Debito",
            Domain.Vendas.Entities.FormaPagamento.Crediario     => "Crediario",
            Domain.Vendas.Entities.FormaPagamento.Boleto        => "Boleto",
            Domain.Vendas.Entities.FormaPagamento.Cheque        => "Cheque",
            Domain.Vendas.Entities.FormaPagamento.Vale          => "Vale",
            _ => f.ToString(),
        };

        // Cada linha é UM texto monoespaçado: rótulo à esquerda + valor à direita
        // com preenchimento por espaços. Assim o valor SEMPRE aparece e fica
        // alinhado, sem depender de colunas/AlignRight (que estavam cortando).
        const int larg = 32;
        static string Linha(string esq, string dir)
        {
            if (esq.Length + dir.Length + 1 > larg)
                esq = esq[..Math.Max(0, larg - dir.Length - 1)];
            return esq + new string(' ', Math.Max(1, larg - esq.Length - dir.Length)) + dir;
        }
        var traco = new string('-', larg);
        var brl = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

        RegistrarFonteMono();
        var fonteMono = _fonteMonoDisponivel ? "DejaVu Sans Mono" : null;

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.ContinuousSize(80, Unit.Millimetre);  // bobina térmica 80mm, altura contínua
                page.Margin(4, Unit.Millimetre);
                page.DefaultTextStyle(t => fonteMono != null ? t.FontSize(9).FontFamily(fonteMono) : t.FontSize(9));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text(empresa?.RazaoSocial ?? "LOJA").Bold().FontSize(11);
                    if (!string.IsNullOrWhiteSpace(empresa?.Cnpj))
                        col.Item().AlignCenter().Text($"CNPJ {empresa!.Cnpj}").FontSize(8);
                    col.Item().PaddingTop(2).AlignCenter().Text("COMPROVANTE DE VENDA").Bold();
                    col.Item().AlignCenter().Text($"Venda {venda.Numero} - {venda.DataHora:dd/MM/yy HH:mm}").FontSize(8);
                    col.Item().Text(traco);

                    foreach (var item in venda.Itens)
                    {
                        col.Item().Text(item.Descricao);
                        col.Item().Text(Linha($"  {item.Quantidade.ToString("0.###", brl)} x {item.PrecoUnitario.ToString("N2", brl)}",
                                              item.Total.ToString("N2", brl)));
                    }

                    col.Item().Text(traco);
                    col.Item().Text(Linha("TOTAL", "R$ " + venda.Total.ToString("N2", brl))).Bold();

                    col.Item().PaddingTop(2).Text("Pagamento:").FontSize(8);
                    foreach (var pag in venda.Pagamentos)
                        col.Item().Text(Linha(FormaTexto(pag.Forma), pag.Valor.ToString("N2", brl)));
                    if (venda.Troco > 0)
                        col.Item().Text(Linha("Troco", venda.Troco.ToString("N2", brl)));

                    col.Item().Text(traco);
                    col.Item().PaddingTop(4).AlignCenter().Text("Obrigado pela preferencia!").Italic().FontSize(8);
                    col.Item().AlignCenter().Text("Documento sem valor fiscal").FontSize(7);
                });
            });
        });

        return File(pdf.GeneratePdf(), "application/pdf", $"recibo-venda-{venda.Numero}.pdf");
    }

    // Registra a fonte monoespaçada do sistema (Linux) uma única vez, para o
    // cupom ficar alinhado. Se não achar, o QuestPDF usa a fonte padrão.
    private static bool _fonteMonoTentada;
    private static bool _fonteMonoDisponivel;
    private static void RegistrarFonteMono()
    {
        if (_fonteMonoTentada) return;
        _fonteMonoTentada = true;
        try
        {
            string[] arquivos =
            {
                "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf",
            };
            foreach (var a in arquivos)
                if (System.IO.File.Exists(a))
                    QuestPDF.Drawing.FontManager.RegisterFont(System.IO.File.OpenRead(a));
            _fonteMonoDisponivel = System.IO.File.Exists(arquivos[0]);
        }
        catch { _fonteMonoDisponivel = false; }
    }
}
