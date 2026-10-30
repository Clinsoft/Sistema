using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Geração de DANFE A4 retrato (layout oficial SEFAZ) usando QuestPDF.
/// </summary>
public class DanfeService : IDanfeService
{
    public byte[] GerarDanfe(NotaFiscal nota, Empresa empresa)
    {
        var br = new System.Globalization.CultureInfo("pt-BR");
        string M(decimal v) => v.ToString("N2", br);
        string NumFmt(long n) => n.ToString("000000000").Insert(6, ".").Insert(3, ".");
        var chave = new string((nota.ChaveAcesso ?? "").Where(char.IsDigit).ToArray());
        var chaveFmt = chave.Length == 44
            ? string.Join(" ", Enumerable.Range(0, 11).Select(i => chave.Substring(i * 4, 4)))
            : chave;
        var entrada = nota.Finalidade == 4 || nota.NaturezaOperacao == NaturezaOperacao.Devolucao;
        var natOp = entrada ? "DEVOLUÇÃO DE COMPRA PARA COMERCIALIZAÇÃO" : DescricaoNatureza(nota.NaturezaOperacao);
        var simples = nota.Itens.Any(i => !string.IsNullOrWhiteSpace(i.CsosnIcms));
        var bcIcmsTot = nota.Itens.Sum(i => i.BaseIcms);
        var vIcmsTot = nota.Itens.Sum(i => i.ValorIcms);
        byte[]? logo = CarregarLogo();
        var barras = chave.Length == 44 ? Code128C(chave) : null;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12);
                page.DefaultTextStyle(ts => ts.FontSize(7f).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // Barra de título cinza de uma seção.
                    void Secao(string t) => col.Item().Background("#E6E6E6").Border(0.6f)
                        .PaddingHorizontal(4).PaddingVertical(1).Text(t).Bold().FontSize(6);

                    // ═══ CANHOTO (recibo de entrega) ═══
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(8).Border(0.6f).PaddingHorizontal(4).PaddingVertical(3).Column(c =>
                        {
                            c.Item().Text($"RECEBEMOS DE {empresa.RazaoSocial} OS PRODUTOS / SERVIÇOS CONSTANTES DA NOTA FISCAL ELETRÔNICA INDICADA AO LADO").FontSize(6);
                            c.Item().PaddingTop(8).Row(r =>
                            {
                                r.RelativeItem(3).BorderTop(0.6f).PaddingTop(1).Text("DATA DE RECEBIMENTO").FontSize(5.5f).FontColor("#555555");
                                r.RelativeItem(7).PaddingLeft(4).BorderTop(0.6f).PaddingTop(1).Text("IDENTIFICAÇÃO E ASSINATURA DO RECEBEDOR").FontSize(5.5f).FontColor("#555555");
                            });
                        });
                        row.RelativeItem(2).Border(0.6f).AlignMiddle().PaddingVertical(4).Column(c =>
                        {
                            c.Item().AlignCenter().Text("NF-e").Bold().FontSize(12);
                            c.Item().AlignCenter().Text($"Nº {NumFmt(nota.Numero)}").FontSize(7);
                            c.Item().AlignCenter().Text($"SÉRIE {nota.Serie:000}").FontSize(7);
                        });
                    });
                    col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor("#999999");

                    // ═══ CABEÇALHO: Emitente | DANFE | Chave ═══
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(42).Border(0.6f).Padding(4).Column(c =>
                        {
                            c.Item().AlignCenter().Text("IDENTIFICAÇÃO DO EMITENTE").FontSize(5.5f).FontColor("#555555");
                            c.Item().PaddingTop(2).Row(er =>
                            {
                                if (logo is not null)
                                    er.ConstantItem(46).PaddingRight(5).AlignMiddle().Image(logo).FitArea();
                                er.RelativeItem().Column(x =>
                                {
                                    x.Item().Text(empresa.NomeFantasia).Bold().FontSize(11);
                                    x.Item().Text(empresa.RazaoSocial).FontSize(6.5f);
                                    x.Item().PaddingTop(2).Text($"{empresa.Logradouro}, {empresa.Numero}"
                                        + (string.IsNullOrEmpty(empresa.Complemento) ? "" : " " + empresa.Complemento)).FontSize(6.5f);
                                    x.Item().Text($"{empresa.Bairro} - {empresa.Cidade}/{empresa.Uf}").FontSize(6.5f);
                                    x.Item().Text($"CEP: {empresa.Cep}   Fone: {empresa.Telefone}").FontSize(6.5f);
                                });
                            });
                        });

                        row.RelativeItem(20).Border(0.6f).Padding(4).Column(c =>
                        {
                            c.Item().AlignCenter().Text("DANFE").Bold().FontSize(12);
                            c.Item().AlignCenter().Text("Documento Auxiliar da").FontSize(5.5f);
                            c.Item().AlignCenter().Text("Nota Fiscal Eletrônica").FontSize(5.5f);
                            c.Item().PaddingTop(3).Row(r =>
                            {
                                r.RelativeItem().AlignRight().PaddingRight(3).Column(x =>
                                {
                                    x.Item().Text("0 - ENTRADA").FontSize(5.5f);
                                    x.Item().Text("1 - SAÍDA").FontSize(5.5f);
                                });
                                r.ConstantItem(16).AlignMiddle().Border(0.6f).PaddingVertical(2).AlignCenter().Text(entrada ? "0" : "1").Bold().FontSize(10);
                            });
                            c.Item().PaddingTop(3).AlignCenter().Text($"Nº {NumFmt(nota.Numero)}").Bold().FontSize(8);
                            c.Item().AlignCenter().Text($"SÉRIE {nota.Serie:000}").FontSize(7);
                            c.Item().AlignCenter().Text("FOLHA 1/1").FontSize(6);
                        });

                        row.RelativeItem(38).Border(0.6f).Padding(4).Column(c =>
                        {
                            if (barras is not null)
                                c.Item().Height(28).Row(bc =>
                                {
                                    foreach (var (w, bar) in barras)
                                    {
                                        var seg = bc.ConstantItem(w * 0.62f);
                                        if (bar) seg.Background("#000000");
                                    }
                                });
                            c.Item().PaddingTop(3).AlignCenter().Text("CHAVE DE ACESSO").Bold().FontSize(5.5f);
                            c.Item().AlignCenter().Text(chaveFmt).FontSize(7f).FontFamily("Courier New");
                            c.Item().PaddingTop(3).AlignCenter().Text("Consulta de autenticidade no portal nacional da NF-e www.nfe.fazenda.gov.br/portal ou no site da Sefaz autorizadora").FontSize(5.5f).Italic();
                        });
                    });

                    // ═══ NATUREZA + PROTOCOLO ═══
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(6), "NATUREZA DA OPERAÇÃO", natOp);
                        Campo(row.RelativeItem(4), "PROTOCOLO DE AUTORIZAÇÃO DE USO",
                            string.IsNullOrEmpty(nota.Protocolo) ? "—" : nota.Protocolo);
                    });
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(4), "INSCRIÇÃO ESTADUAL", empresa.InscricaoEstadual);
                        Campo(row.RelativeItem(3), "INSC. ESTADUAL DO SUBST. TRIB.", "");
                        Campo(row.RelativeItem(3), "CNPJ", FormatarCnpj(empresa.Cnpj));
                    });

                    // ═══ DESTINATÁRIO / REMETENTE ═══
                    Secao("DESTINATÁRIO / REMETENTE");
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(6), "NOME / RAZÃO SOCIAL", nota.NomeDestinatario ?? "CONSUMIDOR FINAL");
                        Campo(row.RelativeItem(2), "CNPJ / CPF", FormatarCpfCnpj(nota.CpfCnpjDestinatario ?? nota.CpfCnpjConsumidor ?? ""));
                        Campo(row.RelativeItem(2), "DATA DA EMISSÃO", nota.DataEmissao.ToString("dd/MM/yyyy"));
                    });
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(5), "ENDEREÇO", string.IsNullOrWhiteSpace(nota.LogradouroDest) ? "" : $"{nota.LogradouroDest}, {nota.NumeroDest}");
                        Campo(row.RelativeItem(3), "BAIRRO / DISTRITO", nota.BairroDest ?? "");
                        Campo(row.RelativeItem(2), "CEP", FormatarCep(nota.CepDest));
                        Campo(row.RelativeItem(2), "DATA SAÍDA/ENTRADA", (nota.DataSaida ?? nota.DataEmissao).ToString("dd/MM/yyyy"));
                    });
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(4), "MUNICÍPIO", nota.MunicipioDest ?? "");
                        Campo(row.RelativeItem(1), "UF", nota.UfDest ?? "");
                        Campo(row.RelativeItem(3), "FONE / FAX", "");
                        Campo(row.RelativeItem(3), "INSCRIÇÃO ESTADUAL", nota.IeDestinatario ?? "");
                    });

                    // ═══ FATURA / DUPLICATAS ═══
                    Secao("FATURA / DUPLICATAS");
                    col.Item().Border(0.6f).MinHeight(16);

                    // ═══ CÁLCULO DO IMPOSTO ═══
                    Secao("CÁLCULO DO IMPOSTO");
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(), "BASE DE CÁLCULO DO ICMS", M(bcIcmsTot));
                        Campo(row.RelativeItem(), "VALOR DO ICMS", M(vIcmsTot));
                        Campo(row.RelativeItem(), "BASE CÁLC. ICMS ST", M(0));
                        Campo(row.RelativeItem(), "VALOR DO ICMS ST", M(0));
                        Campo(row.RelativeItem(), "VALOR TOTAL DOS PRODUTOS", M(nota.TotalProdutos));
                    });
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(), "VALOR DO FRETE", M(0));
                        Campo(row.RelativeItem(), "VALOR DO SEGURO", M(0));
                        Campo(row.RelativeItem(), "DESCONTO", M(nota.TotalDesconto));
                        Campo(row.RelativeItem(), "OUTRAS DESPESAS", M(0));
                        Campo(row.RelativeItem(), "VALOR DO IPI", M(0));
                        Campo(row.RelativeItem(), "VALOR TOTAL DA NOTA", M(nota.TotalNota), false, true);
                    });

                    // ═══ TRANSPORTADOR / VOLUMES ═══
                    Secao("TRANSPORTADOR / VOLUMES TRANSPORTADOS");
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(4), "RAZÃO SOCIAL", "");
                        Campo(row.RelativeItem(3), "FRETE POR CONTA", "9 - Sem Frete");
                        Campo(row.RelativeItem(2), "PLACA DO VEÍCULO", "");
                        Campo(row.RelativeItem(1), "UF", "");
                        Campo(row.RelativeItem(3), "CNPJ / CPF", "");
                    });
                    col.Item().Row(row =>
                    {
                        Campo(row.RelativeItem(2), "QUANTIDADE", "");
                        Campo(row.RelativeItem(3), "ESPÉCIE", "");
                        Campo(row.RelativeItem(3), "MARCA", "");
                        Campo(row.RelativeItem(2), "PESO BRUTO", "");
                        Campo(row.RelativeItem(2), "PESO LÍQUIDO", "");
                    });

                    // ═══ DADOS DOS PRODUTOS / SERVIÇOS ═══
                    Secao("DADOS DOS PRODUTOS / SERVIÇOS");
                    col.Item().PaddingRight(16).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);   // Código
                            cols.RelativeColumn(3);    // Descrição
                            cols.ConstantColumn(40);   // NCM
                            cols.ConstantColumn(30);   // CST/CSOSN
                            cols.ConstantColumn(24);   // CFOP
                            cols.ConstantColumn(16);   // UN
                            cols.ConstantColumn(34);   // Qtd
                            cols.ConstantColumn(42);   // V.Unit
                            cols.ConstantColumn(44);   // V.Total
                            cols.ConstantColumn(42);   // BC ICMS
                            cols.ConstantColumn(40);   // V.ICMS
                            cols.ConstantColumn(28);   // Alíq
                        });

                        // Sem AlignRight: em Table o QuestPDF descarta silenciosamente o
                        // conteúdo da célula alinhada à direita quando fica apertada.
                        static IContainer HCell(IContainer c) => c.Background("#E6E6E6").Border(0.4f).PaddingVertical(2).PaddingHorizontal(2).DefaultTextStyle(t => t.FontSize(5.5f).Bold());
                        table.Header(h =>
                        {
                            h.Cell().Element(HCell).Text("CÓD.");
                            h.Cell().Element(HCell).Text("DESCRIÇÃO DO PRODUTO / SERVIÇO");
                            h.Cell().Element(HCell).Text("NCM/SH");
                            h.Cell().Element(HCell).Text("CST/CSOSN");
                            h.Cell().Element(HCell).Text("CFOP");
                            h.Cell().Element(HCell).Text("UN");
                            h.Cell().Element(HCell).Text("QTD");
                            h.Cell().Element(HCell).Text("V.UNIT");
                            h.Cell().Element(HCell).Text("V.TOTAL");
                            h.Cell().Element(HCell).Text("BC ICMS");
                            h.Cell().Element(HCell).Text("V.ICMS");
                            h.Cell().Element(HCell).Text("ALÍQ");
                        });

                        static IContainer DCell(IContainer c) => c.BorderVertical(0.4f).BorderBottom(0.3f).PaddingVertical(2).PaddingHorizontal(2).DefaultTextStyle(t => t.FontSize(5.5f));
                        foreach (var item in nota.Itens)
                        {
                            table.Cell().Element(DCell).Text(item.Codigo);
                            table.Cell().Element(DCell).Text(item.Descricao);
                            table.Cell().Element(DCell).Text(item.Ncm ?? "");
                            table.Cell().Element(DCell).Text(item.CsosnIcms ?? item.CstIcms ?? "");
                            table.Cell().Element(DCell).Text(item.Cfop);
                            table.Cell().Element(DCell).Text(item.UnidadeMedida);
                            table.Cell().Element(DCell).Text(item.Pesavel ? item.Quantidade.ToString("N3", br) : item.Quantidade.ToString("N0", br));
                            table.Cell().Element(DCell).Text(item.ValorUnitario.ToString("N2", br));
                            table.Cell().Element(DCell).Text(item.ValorTotal.ToString("N2", br));
                            table.Cell().Element(DCell).Text(item.BaseIcms.ToString("N2", br));
                            table.Cell().Element(DCell).Text(item.ValorIcms.ToString("N2", br));
                            table.Cell().Element(DCell).Text(item.AliquotaIcms.ToString("N2", br));
                        }
                    });

                    // ═══ DADOS ADICIONAIS ═══
                    Secao("DADOS ADICIONAIS");
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(7).Border(0.6f).Padding(4).MinHeight(80).Column(c =>
                        {
                            c.Item().Text("INFORMAÇÕES COMPLEMENTARES").FontSize(5.5f).FontColor("#555555");
                            if (entrada)
                            {
                                var chRef = new string((nota.ChaveReferenciada ?? "").Where(char.IsDigit).ToArray());
                                if (chRef.Length == 44)
                                {
                                    var serieRef = int.Parse(chRef[22..25]).ToString();
                                    var numRef = long.Parse(chRef[25..34]).ToString();
                                    var forn = string.IsNullOrWhiteSpace(nota.NomeDestinatario) ? "fornecedor" : nota.NomeDestinatario;
                                    c.Item().PaddingTop(1).Text($"Devolução de mercadoria referente à NF-e nº {numRef}, série {serieRef}, chave {chRef}, emitida por {forn}.").FontSize(6.5f);
                                }
                                else
                                {
                                    c.Item().PaddingTop(1).Text("Nota de devolução de compra. Referência à NF-e de entrada informada no campo próprio.").FontSize(6.5f);
                                }
                                if (simples)
                                    c.Item().PaddingTop(1).Text("DOCUMENTO EMITIDO POR ME OU EPP OPTANTE PELO SIMPLES NACIONAL. NAO GERA DIREITO A CREDITO FISCAL DE IPI.").FontSize(6.5f);
                            }
                            c.Item().PaddingTop(1).Text($"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}.").FontSize(6.5f);
                        });
                        row.RelativeItem(3).Border(0.6f).Padding(4).Column(c =>
                        {
                            c.Item().Text("RESERVADO AO FISCO").FontSize(5.5f).FontColor("#555555");
                        });
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
            c.Item().Text(label).FontSize(5.5f).FontColor("#555555");
            if (destaque) c.Item().Text(valor).FontSize(8).Bold();
            else c.Item().Text(valor).FontSize(7f);
        });
    }

    private static string FormatarCep(string? cep)
    {
        var c = new string((cep ?? "").Where(char.IsDigit).ToArray());
        return c.Length == 8 ? $"{c[..5]}-{c[5..]}" : (cep ?? "");
    }

    // Carrega a logo da empresa da wwwroot (se existir).
    private static byte[]? CarregarLogo()
    {
        try
        {
            var p = System.IO.Path.Combine("wwwroot", Branding.BrandingRuntime.Atual.LogoArquivo);
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
