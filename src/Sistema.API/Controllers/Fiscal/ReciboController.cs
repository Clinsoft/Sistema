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
    /// <summary>Gera/reimprime o cupom fiscal (DANFE NFC-e) térmico 80mm de uma venda autorizada.</summary>
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

        var venda = await db.Vendas.AsNoTracking()
            .Include(v => v.Pagamentos)
            .FirstOrDefaultAsync(v => v.Id == vendaId, ct);
        var vendedor = venda is not null
            ? await db.Usuarios.AsNoTracking().Where(u => u.Id == venda.UsuarioId)
                .Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;

        var pdf = GerarDanfeNFCeTermico(nota, empresa, venda, vendedor);
        return File(pdf, "application/pdf", $"nfce-{nota.Numero:D9}.pdf");
    }

    // Gera o DANFE NFC-e no formato de cupom térmico 80mm (padrão SEFAZ).
    private static byte[] GerarDanfeNFCeTermico(
        Sistema.Domain.Fiscal.Entities.NotaFiscal nota,
        Sistema.Domain.Cadastros.Entities.Empresa empresa,
        Sistema.Domain.Vendas.Entities.Venda? venda,
        string? vendedor)
    {
        var brl = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        const int larg = 36;   // caracteres que cabem na bobina 80mm (sem quebra de linha)
        static string Linha(string esq, string dir)
        {
            if (esq.Length + dir.Length + 1 > larg)
                esq = esq[..Math.Max(0, larg - dir.Length - 1)];
            return esq + new string(' ', Math.Max(1, larg - esq.Length - dir.Length)) + dir;
        }
        var traco = new string('-', larg);

        // QR Code (imagem PNG) a partir da URL do QR da nota.
        byte[]? qrPng = null;
        if (!string.IsNullOrEmpty(nota.QrCode))
        {
            using var qrGen = new QRCoder.QRCodeGenerator();
            var qrData = qrGen.CreateQrCode(nota.QrCode, QRCoder.QRCodeGenerator.ECCLevel.M);
            qrPng = new QRCoder.PngByteQRCode(qrData).GetGraphic(10);
        }

        var chave = nota.ChaveAcesso ?? "";
        var chaveFmt = string.Join(" ", Enumerable.Range(0, (chave.Length + 3) / 4)
            .Select(i => chave.Substring(i * 4, Math.Min(4, chave.Length - i * 4))));
        var doc = nota.CpfCnpjDestinatario ?? nota.CpfCnpjConsumidor;

        RegistrarFonteMono();
        var fonteMono = _fonteMonoDisponivel ? "DejaVu Sans Mono" : "Courier New";

        var pdf = Document.Create(d =>
        {
            d.Page(page =>
            {
                page.ContinuousSize(80, Unit.Millimetre);
                page.Margin(4, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(8).FontFamily(fonteMono));

                page.Content().Column(col =>
                {
                    // Cabeçalho do emitente (centralizado)
                    col.Item().AlignCenter().Text(empresa.RazaoSocial).Bold().FontSize(9);
                    col.Item().AlignCenter().Text($"CNPJ: {empresa.Cnpj}").FontSize(7);
                    col.Item().AlignCenter().Text($"IE: {empresa.InscricaoEstadual}").FontSize(7);
                    col.Item().AlignCenter().Text(
                        $"{empresa.Logradouro}, {empresa.Numero} - {empresa.Bairro}").FontSize(7);
                    col.Item().AlignCenter().Text($"{empresa.Cidade} - {empresa.Uf}").FontSize(7);
                    col.Item().Text(traco);
                    col.Item().AlignCenter().Text("DANFE NFC-e").Bold().FontSize(8);
                    col.Item().AlignCenter().Text(
                        "Documento Auxiliar da Nota Fiscal de Consumidor Eletronica").FontSize(7);
                    col.Item().Text(traco);

                    // Itens
                    foreach (var item in nota.Itens)
                    {
                        col.Item().Text($"{item.Codigo} {item.Descricao}");
                        var qtd = item.Pesavel ? item.Quantidade.ToString("N3", brl) : item.Quantidade.ToString("N0", brl);
                        col.Item().Text(Linha(
                            $"  {qtd} {item.UnidadeMedida} x {item.ValorUnitario.ToString("N2", brl)}",
                            item.ValorTotal.ToString("N2", brl)));
                    }
                    col.Item().Text(traco);

                    // Totais (rótulo e valor na mesma linha)
                    col.Item().Text(Linha("Qtde. total de itens", nota.Itens.Count.ToString()));
                    col.Item().Text(Linha("Valor total R$", nota.TotalNota.ToString("N2", brl)));
                    col.Item().Text(Linha("Valor a Pagar R$", nota.TotalNota.ToString("N2", brl))).Bold();
                    col.Item().Text(traco);

                    // Pagamentos
                    col.Item().Text(Linha("FORMA PAGTO", "VALOR R$"));
                    if (venda is not null)
                    {
                        foreach (var pag in venda.Pagamentos)
                            col.Item().Text(Linha(FormaPagamentoTexto(pag.Forma), pag.Valor.ToString("N2", brl)));
                        if (venda.Troco > 0)
                            col.Item().Text(Linha("Troco", "R$ " + venda.Troco.ToString("N2", brl)));
                    }
                    col.Item().Text(traco);

                    // Rodapé fiscal (centralizado)
                    if (!string.IsNullOrWhiteSpace(vendedor))
                        col.Item().AlignCenter().Text($"Vendedor: {vendedor}").FontSize(7);
                    col.Item().AlignCenter().Text(empresa.NomeFantasia).FontSize(7);
                    col.Item().AlignCenter().Text("Trib aprox: Sem parametros p/ calculo").FontSize(7);
                    col.Item().AlignCenter().Text($"Numero: {nota.Numero}  Serie: {nota.Serie}").FontSize(7);
                    col.Item().AlignCenter().Text($"Emissao: {nota.DataEmissao:dd/MM/yyyy HH:mm:ss}").FontSize(7);
                    col.Item().AlignCenter().Text("Via consumidor").FontSize(7);
                    col.Item().PaddingTop(2).AlignCenter().Text("Consulte pela chave de acesso em").FontSize(7);
                    if (!string.IsNullOrEmpty(nota.UrlConsultaQrCode))
                        col.Item().AlignCenter().Text(nota.UrlConsultaQrCode).FontSize(7);
                    col.Item().PaddingTop(2).AlignCenter().Text("CHAVE DE ACESSO").Bold().FontSize(7);
                    col.Item().AlignCenter().Text(chaveFmt).FontSize(7);
                    col.Item().PaddingTop(2).AlignCenter().Text(
                        string.IsNullOrWhiteSpace(doc) ? "Consumidor nao identificado" : $"Consumidor: {doc}").FontSize(7);
                    if (!string.IsNullOrEmpty(nota.Protocolo))
                    {
                        col.Item().Text(traco);
                        col.Item().AlignCenter().Text("Protocolo de Autorizacao").FontSize(7);
                        col.Item().AlignCenter().Text(
                            $"{nota.Protocolo}  {nota.DataEmissao:dd/MM/yyyy HH:mm:ss}").FontSize(7);
                    }

                    // QR Code
                    if (qrPng is not null)
                    {
                        col.Item().Text(traco);
                        col.Item().AlignCenter().Text("CONSULTA VIA LEITOR DE QRCODE").FontSize(7);
                        col.Item().PaddingTop(3).AlignCenter().Width(38, Unit.Millimetre).Image(qrPng);
                    }
                });
            });
        });

        return pdf.GeneratePdf();
    }

    private static string FormaPagamentoTexto(Sistema.Domain.Vendas.Entities.FormaPagamento f) => f switch
    {
        Sistema.Domain.Vendas.Entities.FormaPagamento.Dinheiro      => "Dinheiro",
        Sistema.Domain.Vendas.Entities.FormaPagamento.Pix           => "Pix",
        Sistema.Domain.Vendas.Entities.FormaPagamento.CartaoCredito => "Cartao Credito",
        Sistema.Domain.Vendas.Entities.FormaPagamento.CartaoDebito  => "Cartao Debito",
        Sistema.Domain.Vendas.Entities.FormaPagamento.Crediario     => "Crediario",
        Sistema.Domain.Vendas.Entities.FormaPagamento.Boleto        => "Boleto",
        Sistema.Domain.Vendas.Entities.FormaPagamento.Cheque        => "Cheque",
        Sistema.Domain.Vendas.Entities.FormaPagamento.Vale          => "Vale",
        _ => f.ToString(),
    };

    /// <summary>Gera o cupom de fechamento de caixa (térmico 80mm, padrão do cupom fiscal).</summary>
    [HttpGet("sessao/{sessaoId:guid}")]
    public async Task<IActionResult> ReciboFechamentoSessao(Guid sessaoId, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var sessao = await db.PDVSessoes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessaoId, ct);
        if (sessao is null) return NotFound(new { mensagem = "Sessão de caixa não encontrada." });

        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == sessao.EmpresaId, ct);
        if (empresa is null) return NotFound(new { mensagem = "Empresa não encontrada." });

        var operador = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == sessao.UsuarioId).Select(u => u.Nome).FirstOrDefaultAsync(ct);
        var local = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.Id == sessao.LocalEstoqueId).Select(l => l.Nome).FirstOrDefaultAsync(ct);

        // Vendas por forma de pagamento no período da sessão.
        var grupos = await db.PagamentosVenda.AsNoTracking()
            .Join(db.Vendas, p => p.VendaId, v => v.Id,
                (p, v) => new { p.Forma, p.Valor, v.Status, v.DataHora, v.EmpresaId, v.UsuarioId, v.LocalEstoqueId })
            .Where(x => x.EmpresaId == sessao.EmpresaId
                && x.UsuarioId == sessao.UsuarioId
                && x.LocalEstoqueId == sessao.LocalEstoqueId
                && x.Status == Sistema.Domain.Vendas.Entities.StatusVenda.Finalizada
                && x.DataHora >= sessao.Abertura
                && (sessao.Fechamento == null || x.DataHora <= sessao.Fechamento))
            .GroupBy(x => x.Forma).Select(g => new { forma = g.Key, total = g.Sum(x => x.Valor) })
            .ToListAsync(ct);
        decimal Forma(Sistema.Domain.Vendas.Entities.FormaPagamento f) =>
            grupos.FirstOrDefault(g => g.forma == f)?.total ?? 0m;

        var pdf = GerarFechamentoTermico(sessao, empresa, operador, local,
            Forma(Sistema.Domain.Vendas.Entities.FormaPagamento.Dinheiro),
            Forma(Sistema.Domain.Vendas.Entities.FormaPagamento.Pix),
            Forma(Sistema.Domain.Vendas.Entities.FormaPagamento.CartaoCredito),
            Forma(Sistema.Domain.Vendas.Entities.FormaPagamento.CartaoDebito),
            Forma(Sistema.Domain.Vendas.Entities.FormaPagamento.Crediario));
        return File(pdf, "application/pdf", "fechamento-caixa.pdf");
    }

    private static byte[] GerarFechamentoTermico(
        Sistema.Domain.Vendas.Entities.PDVSessao s,
        Sistema.Domain.Cadastros.Entities.Empresa empresa,
        string? operador, string? local,
        decimal dinheiro, decimal pix, decimal credito, decimal debito, decimal crediario)
    {
        var brl = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        const int larg = 36;
        static string Linha(string esq, string dir)
        {
            if (esq.Length + dir.Length + 1 > larg)
                esq = esq[..Math.Max(0, larg - dir.Length - 1)];
            return esq + new string(' ', Math.Max(1, larg - esq.Length - dir.Length)) + dir;
        }
        var traco = new string('-', larg);
        string M(decimal v) => v.ToString("N2", brl);

        // Saldo esperado em dinheiro na gaveta e diferença do conferido.
        var esperadoDinheiro = s.SaldoAbertura + dinheiro + s.TotalSuprimentos - s.TotalSangrias;
        var fechado = s.Fechamento != null;
        var diferenca = fechado ? s.SaldoFechamento - esperadoDinheiro : 0m;

        RegistrarFonteMono();
        var fonteMono = _fonteMonoDisponivel ? "DejaVu Sans Mono" : "Courier New";

        var pdf = Document.Create(d =>
        {
            d.Page(page =>
            {
                page.ContinuousSize(80, Unit.Millimetre);
                page.Margin(4, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(8).FontFamily(fonteMono));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text(empresa.NomeFantasia).Bold().FontSize(9);
                    col.Item().AlignCenter().Text("FECHAMENTO DE CAIXA").Bold().FontSize(8);
                    col.Item().Text(traco);
                    col.Item().Text($"Operador: {operador ?? "-"}").FontSize(7);
                    col.Item().Text($"Caixa: {local ?? "-"}").FontSize(7);
                    col.Item().Text($"Abertura: {s.Abertura:dd/MM/yyyy HH:mm}").FontSize(7);
                    col.Item().Text($"Fechamento: {(s.Fechamento != null ? s.Fechamento.Value.ToString("dd/MM/yyyy HH:mm") : "-")}").FontSize(7);
                    col.Item().Text(traco);

                    col.Item().Text(Linha("Saldo inicial", M(s.SaldoAbertura)));
                    col.Item().Text(Linha("Total de vendas", M(dinheiro + pix + credito + debito + crediario)));
                    col.Item().Text(Linha("Suprimentos", M(s.TotalSuprimentos)));
                    col.Item().Text(Linha("Sangrias", "-" + M(s.TotalSangrias)));
                    col.Item().Text(traco);

                    col.Item().Text("VENDAS POR FORMA").Bold().FontSize(7);
                    col.Item().Text(Linha("Dinheiro", M(dinheiro)));
                    col.Item().Text(Linha("Pix", M(pix)));
                    col.Item().Text(Linha("Cartao Credito", M(credito)));
                    col.Item().Text(Linha("Cartao Debito", M(debito)));
                    if (crediario > 0) col.Item().Text(Linha("Crediario", M(crediario)));
                    col.Item().Text(traco);

                    col.Item().Text(Linha("Esperado em dinheiro", M(esperadoDinheiro)));
                    if (fechado)
                    {
                        col.Item().Text(Linha("Contado (gaveta)", M(s.SaldoFechamento)));
                        col.Item().Text(Linha("Diferenca", (diferenca >= 0 ? "" : "-") + M(Math.Abs(diferenca)))).Bold();
                    }
                    col.Item().Text(traco);

                    if (!string.IsNullOrWhiteSpace(s.ObservacaoFechamento))
                    {
                        col.Item().Text($"Obs: {s.ObservacaoFechamento}").FontSize(7);
                        col.Item().Text(traco);
                    }
                    col.Item().PaddingTop(2).AlignCenter().Text("Conferencia de caixa - sem valor fiscal").FontSize(7);
                });
            });
        });

        return pdf.GeneratePdf();
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
