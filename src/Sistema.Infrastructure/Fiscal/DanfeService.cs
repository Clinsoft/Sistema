using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Geração de DANFE A4 retrato usando QuestPDF.
/// </summary>
public class DanfeService : IDanfeService
{
    public byte[] GerarDanfe(NotaFiscal nota, Empresa empresa)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12);
                page.DefaultTextStyle(ts => ts.FontSize(7).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // ── Cabeçalho ────────────────────────────────────────────
                    col.Item().BorderBottom(1).PaddingBottom(4).Row(row =>
                    {
                        // Emitente
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(empresa.NomeFantasia).Bold().FontSize(11);
                            c.Item().Text(empresa.RazaoSocial).FontSize(8);
                            c.Item().Text($"CNPJ: {FormatarCnpj(empresa.Cnpj)}");
                            c.Item().Text($"IE: {empresa.InscricaoEstadual}");
                            c.Item().Text($"{empresa.Logradouro}, {empresa.Numero}{(empresa.Complemento != null ? " " + empresa.Complemento : "")}");
                            c.Item().Text($"{empresa.Bairro} – {empresa.Cidade}/{empresa.Uf}  CEP: {empresa.Cep}");
                            c.Item().Text($"Tel: {empresa.Telefone}");
                        });

                        // Título central
                        row.RelativeItem(2).AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text("DANFE").Bold().FontSize(10);
                            c.Item().AlignCenter().Text("Documento Auxiliar da").FontSize(7);
                            c.Item().AlignCenter().Text("Nota Fiscal Eletrônica").FontSize(7);
                            c.Item().PaddingTop(4).AlignCenter()
                                .Text($"Modelo: {(int)nota.Modelo}  Série: {nota.Serie:D3}").FontSize(8);
                            c.Item().AlignCenter()
                                .Text($"Número: {nota.Numero:D9}").FontSize(9).Bold();
                            c.Item().PaddingTop(4).AlignCenter()
                                .Text($"Emissão: {nota.DataEmissao:dd/MM/yyyy HH:mm}").FontSize(7);
                        });

                        // Chave de acesso + código de barras
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text("CHAVE DE ACESSO").Bold();
                            var chave = nota.ChaveAcesso ?? "";
                            // Formatação da chave em grupos de 4
                            var chaveFormatada = string.Join(" ", Enumerable.Range(0, chave.Length / 4)
                                .Select(i => chave.Substring(i * 4, Math.Min(4, chave.Length - i * 4))));
                            c.Item().Text(chaveFormatada).FontSize(6.5f).FontFamily("Courier New");
                            // Código de barras representado como texto monospace
                            c.Item().PaddingTop(3).Text("|||" + string.Concat(chave.Select(d => $"| {d}")) + "|||")
                                .FontSize(5).FontFamily("Courier New").LetterSpacing(-0.5f);
                            if (!string.IsNullOrEmpty(nota.Protocolo))
                                c.Item().PaddingTop(2).Text($"Protocolo: {nota.Protocolo}").FontSize(7);
                        });
                    });

                    col.Item().PaddingTop(4);

                    // ── Natureza da Operação ──────────────────────────────────
                    col.Item().Border(0.5f).Padding(3).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("NATUREZA DA OPERAÇÃO").Bold().FontSize(6);
                            c.Item().Text(DescricaoNatureza(nota.NaturezaOperacao)).FontSize(8);
                        });
                    });

                    col.Item().PaddingTop(4);

                    // ── Destinatário ──────────────────────────────────────────
                    col.Item().Border(0.5f).Padding(3).Column(c =>
                    {
                        c.Item().Text("DESTINATÁRIO / REMETENTE").Bold().FontSize(6);
                        c.Item().PaddingTop(2).Row(row =>
                        {
                            row.RelativeItem(3).Column(d =>
                            {
                                d.Item().Text($"Nome/Razão Social: {nota.NomeDestinatario ?? "CONSUMIDOR FINAL"}").FontSize(8);
                            });
                            row.RelativeItem(2).Column(d =>
                            {
                                d.Item().Text($"CPF/CNPJ: {FormatarCpfCnpj(nota.CpfCnpjDestinatario ?? nota.CpfCnpjConsumidor ?? "")}").FontSize(8);
                            });
                        });
                    });

                    col.Item().PaddingTop(4);

                    // ── Itens ─────────────────────────────────────────────────
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(20);   // #
                            cols.ConstantColumn(40);   // Código
                            cols.RelativeColumn(4);    // Descrição
                            cols.ConstantColumn(35);   // NCM
                            cols.ConstantColumn(30);   // CFOP
                            cols.ConstantColumn(20);   // UN
                            cols.ConstantColumn(35);   // Qtd
                            cols.ConstantColumn(45);   // Vl Unit
                            cols.ConstantColumn(45);   // Vl Total
                        });

                        // Cabeçalho da tabela
                        static IContainer HeaderCell(IContainer c) =>
                            c.Background("#EEEEEE").Border(0.3f).Padding(2);

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("#").Bold();
                            header.Cell().Element(HeaderCell).Text("Código").Bold();
                            header.Cell().Element(HeaderCell).Text("Descrição").Bold();
                            header.Cell().Element(HeaderCell).Text("NCM").Bold();
                            header.Cell().Element(HeaderCell).Text("CFOP").Bold();
                            header.Cell().Element(HeaderCell).Text("UN").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Qtd").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Vl Unit").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Vl Total").Bold();
                        });

                        // Linhas de item
                        static IContainer DataCell(IContainer c) =>
                            c.Border(0.3f).Padding(2);

                        var br = new System.Globalization.CultureInfo("pt-BR");
                        foreach (var item in nota.Itens)
                        {
                            table.Cell().Element(DataCell).Text(item.NumeroItem.ToString());
                            table.Cell().Element(DataCell).Text(item.Codigo);
                            table.Cell().Element(DataCell).Text(item.Descricao);
                            table.Cell().Element(DataCell).Text(item.Ncm ?? "");
                            table.Cell().Element(DataCell).Text(item.Cfop);
                            table.Cell().Element(DataCell).Text(item.UnidadeMedida);
                            table.Cell().Element(DataCell).AlignRight()
                                .Text(item.Pesavel ? item.Quantidade.ToString("N3", br) : item.Quantidade.ToString("N0", br));
                            table.Cell().Element(DataCell).AlignRight()
                                .Text(item.ValorUnitario.ToString("N4", br));
                            table.Cell().Element(DataCell).AlignRight()
                                .Text(item.ValorTotal.ToString("N2", br));
                        }
                    });

                    col.Item().PaddingTop(4);

                    // ── Totais ────────────────────────────────────────────────
                    var ptBR = new System.Globalization.CultureInfo("pt-BR");
                    col.Item().Border(0.5f).Padding(3).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("TOTAIS DA NOTA").Bold().FontSize(6);
                            c.Item().PaddingTop(2).Row(r =>
                            {
                                r.RelativeItem().Text($"Total Produtos: {nota.TotalProdutos.ToString("C", ptBR)}");
                                r.RelativeItem().Text($"Desconto: {nota.TotalDesconto.ToString("C", ptBR)}");
                                r.RelativeItem().Text($"Total NF: {nota.TotalNota.ToString("C", ptBR)}").Bold().FontSize(9);
                            });
                        });
                    });

                    col.Item().PaddingTop(4);

                    // ── Tributos ──────────────────────────────────────────────
                    col.Item().Border(0.5f).Padding(3).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("INFORMAÇÕES FISCAIS").Bold().FontSize(6);
                            c.Item().PaddingTop(2).Row(r =>
                            {
                                r.RelativeItem().Text($"Base ICMS: {nota.TotalIcms.ToString("C", ptBR)}");
                                r.RelativeItem().Text($"Vl ICMS: {nota.TotalIcms.ToString("C", ptBR)}");
                                r.RelativeItem().Text($"PIS: {nota.TotalPis.ToString("C", ptBR)}");
                                r.RelativeItem().Text($"COFINS: {nota.TotalCofins.ToString("C", ptBR)}");
                            });
                        });
                    });

                    col.Item().PaddingTop(4);

                    // ── QR Code (NFC-e) ───────────────────────────────────────
                    if (!string.IsNullOrEmpty(nota.QrCode))
                    {
                        col.Item().Border(0.5f).Padding(3).Column(c =>
                        {
                            c.Item().Text("CONSULTE PELA CHAVE DE ACESSO OU QR CODE").Bold().FontSize(6);
                            c.Item().PaddingTop(2).Text(nota.QrCode).FontSize(6).FontFamily("Courier New");
                            if (!string.IsNullOrEmpty(nota.UrlConsultaQrCode))
                                c.Item().PaddingTop(2).Text($"URL: {nota.UrlConsultaQrCode}").FontSize(6);
                        });

                        col.Item().PaddingTop(4);
                    }

                    // ── Protocolo de autorização ──────────────────────────────
                    if (!string.IsNullOrEmpty(nota.Protocolo))
                    {
                        col.Item().Border(0.5f).Padding(3)
                            .Text($"Protocolo de Autorização: {nota.Protocolo}  |  Data: {nota.DataEmissao:dd/MM/yyyy HH:mm:ss}")
                            .FontSize(7);
                    }
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static string FormatarCnpj(string cnpj)
    {
        cnpj = new string(cnpj.Where(char.IsLetterOrDigit).ToArray());
        return cnpj.Length == 14
            ? $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}"
            : cnpj;
    }

    private static string FormatarCpfCnpj(string valor)
    {
        var v = new string(valor.Where(char.IsLetterOrDigit).ToArray());
        return v.Length == 11
            ? $"{v[..3]}.{v[3..6]}.{v[6..9]}-{v[9..]}"
            : FormatarCnpj(valor);
    }

    private static string DescricaoNatureza(NaturezaOperacao natureza) => natureza switch
    {
        NaturezaOperacao.VendaProduto   => "Venda de Produto",
        NaturezaOperacao.VendaConsumidor => "Venda a Consumidor Final",
        NaturezaOperacao.Devolucao      => "Devolução",
        NaturezaOperacao.Transferencia  => "Transferência",
        NaturezaOperacao.Remessa        => "Remessa",
        _ => natureza.ToString()
    };
}
