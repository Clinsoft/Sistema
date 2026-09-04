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
        var br = new System.Globalization.CultureInfo("pt-BR");
        string M(decimal v) => v.ToString("N2", br);
        var chave = new string((nota.ChaveAcesso ?? "").Where(char.IsDigit).ToArray());
        var chaveFmt = chave.Length == 44
            ? string.Join(" ", Enumerable.Range(0, 11).Select(i => chave.Substring(i * 4, 4)))
            : chave;
        var entrada = nota.Finalidade == 4 || nota.NaturezaOperacao == NaturezaOperacao.Devolucao;
        byte[]? logo = CarregarLogo();
        var barras = chave.Length == 44 ? Code128C(chave) : null;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10);
                page.MarginRight(20);
                page.DefaultTextStyle(ts => ts.FontSize(7.5f).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // ═══ CABEÇALHO ═══
                    col.Item().Row(row =>
                    {
                        // Emitente
                        row.RelativeItem(5).Border(0.6f).Padding(6).Row(er =>
                        {
                          if (logo is not null)
                            er.ConstantItem(52).PaddingRight(6).AlignMiddle().Image(logo).FitArea();
                          er.RelativeItem().Column(c =>
                        {
                            c.Item().Text(empresa.NomeFantasia).Bold().FontSize(12);
                            c.Item().Text(empresa.RazaoSocial).FontSize(7.5f);
                            c.Item().PaddingTop(2).Text($"{empresa.Logradouro}, {empresa.Numero}"
                                + (string.IsNullOrEmpty(empresa.Complemento) ? "" : " " + empresa.Complemento));
                            c.Item().Text($"{empresa.Bairro} - {empresa.Cidade}/{empresa.Uf} - CEP {empresa.Cep}");
                            c.Item().Text($"CNPJ: {FormatarCnpj(empresa.Cnpj)}   IE: {empresa.InscricaoEstadual}");
                            c.Item().Text($"Fone: {empresa.Telefone}");
                          });
                        });

                        // Bloco DANFE central
                        row.RelativeItem(2).Border(0.6f).Padding(6).AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text("DANFE").Bold().FontSize(13);
                            c.Item().AlignCenter().Text("Documento Auxiliar da").FontSize(6.5f);
                            c.Item().AlignCenter().Text("Nota Fiscal Eletrônica").FontSize(6.5f);
                            c.Item().PaddingTop(4).AlignCenter().Text($"{(entrada ? "0 - ENTRADA" : "1 - SAÍDA")}").FontSize(7).Bold();
                            c.Item().PaddingTop(3).AlignCenter().Text($"Nº {nota.Numero:000000000}").Bold().FontSize(9);
                            c.Item().AlignCenter().Text($"Série {nota.Serie:000}").FontSize(8);
                        });

                        // Chave de acesso
                        row.RelativeItem(5).Border(0.6f).Padding(6).Column(c =>
                        {
                            if (barras is not null)
                                c.Item().Height(30).Row(bc =>
                                {
                                    foreach (var (w, bar) in barras)
                                    {
                                        var seg = bc.ConstantItem(w * 0.72f);
                                        if (bar) seg.Background("#000000");
                                    }
                                });
                            c.Item().PaddingTop(3).Text("CHAVE DE ACESSO").Bold().FontSize(6);
                            c.Item().Text(chaveFmt).FontSize(8).FontFamily("Courier New");
                            c.Item().PaddingTop(3).Text("Consulta de autenticidade no portal nacional da NF-e www.nfe.fazenda.gov.br/portal ou no site da Sefaz autorizadora").FontSize(6).Italic();
                            if (!string.IsNullOrEmpty(nota.Protocolo))
                                c.Item().PaddingTop(3).Text($"Protocolo: {nota.Protocolo}").FontSize(7).Bold();
                        });
                    });

                    // ═══ NATUREZA DA OPERAÇÃO ═══
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(7), "NATUREZA DA OPERAÇÃO", DescricaoNatureza(nota.NaturezaOperacao)
                            + (entrada ? "  (DEVOLUÇÃO)" : ""));
                        Campo(row.RelativeItem(3), "FINALIDADE", nota.Finalidade == 4 ? "4 - Devolução" : "1 - Normal");
                    });
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(1), "EMISSÃO", nota.DataEmissao.ToString("dd/MM/yyyy"));
                        Campo(row.RelativeItem(2), "MODELO", $"{(int)nota.Modelo}");
                        Campo(row.RelativeItem(4), "DOCUMENTO REFERENCIADO (NF de entrada)",
                            string.IsNullOrEmpty(nota.ChaveReferenciada) ? "-" : FormatarChaveCurta(nota.ChaveReferenciada));
                        Campo(row.RelativeItem(2), "AMBIENTE", "—");
                    });

                    // ═══ DESTINATÁRIO ═══
                    col.Item().Background("#F2F2F2").BorderVertical(0.6f).PaddingHorizontal(4).PaddingVertical(1)
                        .Text("DESTINATÁRIO / REMETENTE").Bold().FontSize(6.5f);
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(6), "NOME / RAZÃO SOCIAL", nota.NomeDestinatario ?? "CONSUMIDOR FINAL");
                        Campo(row.RelativeItem(3), "CNPJ / CPF", FormatarCpfCnpj(nota.CpfCnpjDestinatario ?? nota.CpfCnpjConsumidor ?? ""));
                        Campo(row.RelativeItem(2), "INSCR. ESTADUAL", nota.IeDestinatario ?? "-");
                    });
                    if (!string.IsNullOrWhiteSpace(nota.LogradouroDest))
                    {
                        col.Item().Row(row =>
                        {
                            Campo(row.RelativeItem(6), "ENDEREÇO", $"{nota.LogradouroDest}, {nota.NumeroDest}");
                            Campo(row.RelativeItem(3), "BAIRRO", nota.BairroDest ?? "-");
                            Campo(row.RelativeItem(2), "MUN./UF", $"{nota.MunicipioDest}/{nota.UfDest}");
                        });
                    }

                    // ═══ CÁLCULO DO IMPOSTO ═══
                    col.Item().Background("#F2F2F2").BorderVertical(0.6f).PaddingHorizontal(4).PaddingVertical(1)
                        .Text("CÁLCULO DO IMPOSTO").Bold().FontSize(6.5f);
                    col.Item().Table(imp =>
                    {
                        imp.ColumnsDefinition(c => { for (int i = 0; i < 5; i++) c.RelativeColumn(); });
                        Campo(imp.Cell(), "BASE ICMS", M(nota.TotalIcms), true);
                        Campo(imp.Cell(), "VALOR ICMS", M(nota.TotalIcms), true);
                        Campo(imp.Cell(), "TOTAL PRODUTOS", M(nota.TotalProdutos), true);
                        Campo(imp.Cell(), "DESCONTO", M(nota.TotalDesconto), true);
                        Campo(imp.Cell(), "TOTAL DA NOTA", M(nota.TotalNota), true, true);
                    });

                    // ═══ PRODUTOS / SERVIÇOS ═══
                    col.Item().Background("#F2F2F2").BorderVertical(0.6f).PaddingHorizontal(4).PaddingVertical(1)
                        .Text("DADOS DOS PRODUTOS / SERVIÇOS").Bold().FontSize(6.5f);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(16);   // #
                            cols.ConstantColumn(38);   // Código
                            cols.RelativeColumn(4);    // Descrição
                            cols.ConstantColumn(46);   // NCM
                            cols.ConstantColumn(26);   // CFOP
                            cols.ConstantColumn(18);   // UN
                            cols.ConstantColumn(38);   // Qtd
                            cols.ConstantColumn(48);   // Vl Unit
                            cols.ConstantColumn(52);   // Vl Total
                        });

                        static IContainer HCell(IContainer c) => c.Background("#E6E6E6").Border(0.4f).PaddingVertical(2).PaddingHorizontal(3);
                        table.Header(h =>
                        {
                            h.Cell().Element(HCell).Text("#").Bold();
                            h.Cell().Element(HCell).Text("CÓDIGO").Bold();
                            h.Cell().Element(HCell).Text("DESCRIÇÃO").Bold();
                            h.Cell().Element(HCell).Text("NCM").Bold();
                            h.Cell().Element(HCell).Text("CFOP").Bold();
                            h.Cell().Element(HCell).Text("UN").Bold();
                            h.Cell().Element(HCell).AlignRight().Text("QTD").Bold();
                            h.Cell().Element(HCell).AlignRight().Text("V.UNIT").Bold();
                            h.Cell().Element(HCell).AlignRight().Text("V.TOTAL").Bold();
                        });

                        static IContainer DCell(IContainer c) => c.BorderVertical(0.4f).BorderBottom(0.3f).PaddingVertical(2).PaddingHorizontal(3);
                        foreach (var item in nota.Itens)
                        {
                            table.Cell().Element(DCell).Text(item.NumeroItem.ToString());
                            table.Cell().Element(DCell).Text(item.Codigo);
                            table.Cell().Element(DCell).Text(item.Descricao);
                            table.Cell().Element(DCell).Text(item.Ncm ?? "");
                            table.Cell().Element(DCell).Text(item.Cfop);
                            table.Cell().Element(DCell).Text(item.UnidadeMedida);
                            table.Cell().Element(DCell).AlignRight().Text(item.Pesavel ? item.Quantidade.ToString("N3", br) : item.Quantidade.ToString("N0", br));
                            table.Cell().Element(DCell).AlignRight().Text(item.ValorUnitario.ToString("N2", br));
                            table.Cell().Element(DCell).AlignRight().Text(item.ValorTotal.ToString("N2", br));
                        }
                    });

                    // ═══ DADOS ADICIONAIS ═══
                    col.Item().Background("#F2F2F2").BorderVertical(0.6f).PaddingHorizontal(4).PaddingVertical(1)
                        .Text("DADOS ADICIONAIS").Bold().FontSize(6.5f);
                    col.Item().Border(0.6f).Padding(6).MinHeight(40).Column(c =>
                    {
                        if (entrada)
                            c.Item().Text("Nota de devolução de compra. Referência à NF-e de entrada informada no campo próprio.").FontSize(7);
                        c.Item().Text($"Emitido por EcoGranel em {DateTime.Now:dd/MM/yyyy HH:mm}.").FontSize(7);
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    // Campo rotulado no estilo DANFE (label pequeno em cima, valor embaixo).
    private static void Campo(IContainer container, string label, string valor, bool alignRight = false, bool destaque = false)
    {
        container.Border(0.6f).PaddingHorizontal(4).PaddingVertical(2).Column(c =>
        {
            var lbl = alignRight ? c.Item().AlignRight() : c.Item();
            lbl.Text(label).FontSize(5.5f).FontColor("#555555");
            var t = alignRight ? c.Item().AlignRight() : c.Item();
            if (destaque) t.Text(valor).FontSize(8).Bold();
            else t.Text(valor).FontSize(7.5f);
        });
    }

    private static string FormatarChaveCurta(string chave)
    {
        var c = new string((chave ?? "").Where(char.IsDigit).ToArray());
        return c.Length == 44 ? $"…{c[^12..]}" : c;
    }

    // Carrega a logo da empresa da wwwroot (se existir).
    private static byte[]? CarregarLogo()
    {
        try
        {
            var p = System.IO.Path.Combine("wwwroot", "logo-ecogranel.png");
            return System.IO.File.Exists(p) ? System.IO.File.ReadAllBytes(p) : null;
        }
        catch { return null; }
    }

    // ── Code128-C: gera os segmentos (largura em módulos, barra/espaço) da chave (44 dígitos) ──
    private static readonly string[] Cod128Pat =
    {
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232"
    };
    private static List<(float w, bool bar)> Code128C(string digits)
    {
        var vals = new List<int> { 105 }; // Start C
        for (int i = 0; i + 1 < digits.Length; i += 2)
            vals.Add(int.Parse(digits.Substring(i, 2)));
        long sum = 105;
        for (int k = 1; k < vals.Count; k++) sum += (long)vals[k] * k;
        vals.Add((int)(sum % 103)); // dígito verificador
        var segs = new List<(float, bool)>();
        void Add(string pat) { bool bar = true; foreach (var ch in pat) { segs.Add((ch - '0', bar)); bar = !bar; } }
        foreach (var v in vals) Add(Cod128Pat[v]);
        Add("2331112"); // Stop
        return segs;
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
