using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Fiscal.Entities;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Gera e assina digitalmente o XML da NF-e (modelo 55) ou NFC-e (modelo 65)
/// conforme PL_010d_v1.02 + NT2026.004.
/// Não usa DFe.NET — constrói XML manualmente com XmlWriter.
/// </summary>
public static class NFeXmlBuilder
{
    private const string NsNfe = "http://www.portalfiscal.inf.br/nfe";

    // ─────────────────────────────────────────────────────────────────────────
    // Ponto de entrada público
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gera o XML da NF-e/NFC-e, calcula a chave de acesso, assina digitalmente
    /// e retorna o XML assinado com a chave de 44 dígitos.
    /// </summary>
    public static (string XmlAssinado, string Chave44) GerarXmlAssinado(
        NotaFiscal nota,
        Empresa empresa,
        ConfiguracaoFiscal config,
        IReadOnlyList<(string TPag, decimal VPag)> pagamentos,
        string? informacoesAdicionais = null)
    {
        var chave44 = CalcularChaveAcesso(nota, empresa, config);
        var xmlAssinado = GerarXmlAssinadoComChave(nota, empresa, config, chave44, pagamentos, informacoesAdicionais);
        return (xmlAssinado, chave44);
    }

    /// <summary>
    /// Assina o XML usando uma chave JÁ calculada. Necessário para a NFC-e, onde o
    /// QR Code precisa conter a mesma chave do XML — como CalcularChaveAcesso sorteia
    /// o cNF, a chave tem de ser calculada uma única vez e reaproveitada aqui.
    /// </summary>
    public static string GerarXmlAssinadoComChave(
        NotaFiscal nota,
        Empresa empresa,
        ConfiguracaoFiscal config,
        string chave44,
        IReadOnlyList<(string TPag, decimal VPag)> pagamentos,
        string? informacoesAdicionais = null)
    {
        var xml = ConstruirNFe(nota, empresa, config, chave44, pagamentos, informacoesAdicionais);
        return Assinar(xml, chave44, config);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chave de acesso (44 dígitos)
    // ─────────────────────────────────────────────────────────────────────────

    public static string CalcularChaveAcesso(NotaFiscal nota, Empresa empresa, ConfiguracaoFiscal config)
    {
        var cUF = UfParaCodigo(empresa.Uf).ToString("D2");
        var aamm = nota.DataEmissao.ToString("yyMM");
        var cnpj = ApenasDigitos(empresa.Cnpj).PadLeft(14, '0')[..14];
        var mod = ((int)nota.Modelo).ToString("D2");
        var serie = nota.Serie.ToString("D3");
        var nNF = nota.Numero.ToString("D9");
        var tpEmis = "1"; // normal
        var cNF = new Random().Next(10000000, 99999999).ToString("D8");
        var codMun = (empresa.CodMunicipio ?? "9999999").PadRight(7, '0')[..7];

        var chave = $"{cUF}{aamm}{cnpj}{mod}{serie}{nNF}{tpEmis}{cNF}";
        var dv = CalcularDV(chave);
        return $"{chave}{dv}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Construção do XML NFe
    // ─────────────────────────────────────────────────────────────────────────

    private static string ConstruirNFe(
        NotaFiscal nota, Empresa empresa, ConfiguracaoFiscal config,
        string chave44,
        IReadOnlyList<(string TPag, decimal VPag)> pagamentos,
        string? informacoesAdicionais)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), // sem BOM
            Indent = false
        };

        using (var w = XmlWriter.Create(ms, settings))
        {
            w.WriteStartElement("NFe", NsNfe);

            // ── infNFe ─────────────────────────────────────────────────
            w.WriteStartElement("infNFe");
            w.WriteAttributeString("Id", $"NFe{chave44}");
            w.WriteAttributeString("versao", "4.00");

            EscreverIde(w, nota, empresa, config, chave44);
            EscreverEmit(w, empresa, config);
            EscreverDest(w, nota);
            EscreverItens(w, nota, config);
            EscreverTotal(w, nota);
            EscreverTransp(w);
            EscreverPag(w, pagamentos);

            if (!string.IsNullOrWhiteSpace(informacoesAdicionais))
            {
                w.WriteStartElement("infAdic");
                w.WriteElementString("infCpl", informacoesAdicionais[..Math.Min(5000, informacoesAdicionais.Length)]);
                w.WriteEndElement();
            }

            w.WriteEndElement(); // infNFe

            // infNFeSupl (NFC-e)
            if (nota.Modelo == ModeloNF.NFCe && nota.QrCode is not null)
            {
                w.WriteStartElement("infNFeSupl");
                w.WriteElementString("qrCode", nota.QrCode);
                w.WriteElementString("urlChave", nota.UrlConsultaQrCode ?? "");
                w.WriteEndElement();
            }

            w.WriteEndElement(); // NFe
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void EscreverIde(XmlWriter w, NotaFiscal nota, Empresa empresa,
        ConfiguracaoFiscal config, string chave44)
    {
        var cUF = UfParaCodigo(empresa.Uf);
        var cNF = chave44[35..43]; // posições 35-42 da chave (8 dígitos)
        var tpAmb = config.Ambiente == AmbienteFiscal.Producao ? "1" : "2";
        var mod = (int)nota.Modelo;
        var tpImp = nota.Modelo == ModeloNF.NFCe ? "4" : "1";
        var natOp = nota.NaturezaOperacao switch
        {
            NaturezaOperacao.VendaProduto    => "VENDA DE PRODUTO",
            NaturezaOperacao.VendaConsumidor => "VENDA AO CONSUMIDOR",
            NaturezaOperacao.Devolucao       => "DEVOLUCAO",
            NaturezaOperacao.Transferencia   => "TRANSFERENCIA",
            NaturezaOperacao.Remessa         => "REMESSA",
            _                                => "VENDA"
        };

        w.WriteStartElement("ide");
        w.WriteElementString("cUF",      cUF.ToString());
        w.WriteElementString("cNF",      cNF);
        w.WriteElementString("natOp",    natOp);
        w.WriteElementString("mod",      mod.ToString());
        w.WriteElementString("serie",    nota.Serie.ToString());
        w.WriteElementString("nNF",      nota.Numero.ToString());
        w.WriteElementString("dhEmi",    nota.DataEmissao.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        if (nota.DataSaida.HasValue)
            w.WriteElementString("dhSaiEnt", nota.DataSaida.Value.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        w.WriteElementString("tpNF",     "1"); // 1=saída
        w.WriteElementString("idDest",   "1"); // 1=operação interna
        w.WriteElementString("cMunFG",   empresa.CodMunicipio ?? "9999999");
        w.WriteElementString("tpImp",    tpImp);
        w.WriteElementString("tpEmis",   "1"); // 1=emissão normal
        w.WriteElementString("cDV",      chave44[43].ToString()); // dígito verificador
        w.WriteElementString("tpAmb",    tpAmb);
        w.WriteElementString("finNFe",   "1"); // 1=normal
        w.WriteElementString("indFinal", nota.Modelo == ModeloNF.NFCe ? "1" : "0");
        w.WriteElementString("indPres",  nota.Modelo == ModeloNF.NFCe ? "1" : "9");
        w.WriteElementString("indIntermed", "0");
        w.WriteElementString("procEmi",  "0"); // 0=app contribuinte
        w.WriteElementString("verProc",  "EcoGranel-1.0");
        w.WriteEndElement(); // ide
    }

    private static void EscreverEmit(XmlWriter w, Empresa empresa, ConfiguracaoFiscal config)
    {
        var cnpjDigits = ApenasDigitos(empresa.Cnpj);
        var crt = config.Regime == RegimeTributario.SimplesNacional ? "1" : "3";

        w.WriteStartElement("emit");
        w.WriteElementString("CNPJ",    cnpjDigits.PadLeft(14, '0')[..14]);
        w.WriteElementString("xNome",   empresa.RazaoSocial[..Math.Min(60, empresa.RazaoSocial.Length)]);
        w.WriteElementString("xFant",   empresa.NomeFantasia[..Math.Min(60, empresa.NomeFantasia.Length)]);

        w.WriteStartElement("enderEmit");
        w.WriteElementString("xLgr",   empresa.Logradouro[..Math.Min(60, empresa.Logradouro.Length)]);
        w.WriteElementString("nro",    empresa.Numero[..Math.Min(60, empresa.Numero.Length)]);
        if (!string.IsNullOrEmpty(empresa.Complemento))
            w.WriteElementString("xCpl", empresa.Complemento[..Math.Min(60, empresa.Complemento.Length)]);
        w.WriteElementString("xBairro", empresa.Bairro[..Math.Min(60, empresa.Bairro.Length)]);
        w.WriteElementString("cMun",    empresa.CodMunicipio ?? "9999999");
        w.WriteElementString("xMun",    (empresa.NomeMunicipio ?? empresa.Cidade)[..Math.Min(60, (empresa.NomeMunicipio ?? empresa.Cidade).Length)]);
        w.WriteElementString("UF",      empresa.Uf.ToUpper());
        w.WriteElementString("CEP",     ApenasDigitos(empresa.Cep).PadLeft(8, '0')[..8]);
        w.WriteElementString("cPais",   "1058");
        w.WriteElementString("xPais",   "Brasil");
        if (!string.IsNullOrWhiteSpace(empresa.Telefone))
            w.WriteElementString("fone", ApenasDigitos(empresa.Telefone));
        w.WriteEndElement(); // enderEmit

        w.WriteElementString("IE",         ApenasDigitos(empresa.InscricaoEstadual));
        w.WriteElementString("indIEDest",  "1"); // 1=contribuinte ICMS
        w.WriteElementString("CRT",        crt);
        w.WriteEndElement(); // emit
    }

    private static void EscreverDest(XmlWriter w, NotaFiscal nota)
    {
        // NFC-e sem destinatário identificado
        if (nota.Modelo == ModeloNF.NFCe && string.IsNullOrWhiteSpace(nota.CpfCnpjDestinatario))
        {
            w.WriteStartElement("dest");
            w.WriteElementString("xNome", "NF-e emitida para consumidor final");
            w.WriteElementString("indIEDest", "9");
            w.WriteEndElement();
            return;
        }

        if (string.IsNullOrWhiteSpace(nota.CpfCnpjDestinatario)) return;

        var doc = ApenasDigitos(nota.CpfCnpjDestinatario ?? "");
        w.WriteStartElement("dest");
        if (doc.Length == 11)
            w.WriteElementString("CPF", doc);
        else if (doc.Length == 14)
            w.WriteElementString("CNPJ", doc);
        else
            w.WriteElementString("idEstrangeiro", doc[..Math.Min(20, doc.Length)]);

        if (!string.IsNullOrWhiteSpace(nota.NomeDestinatario))
            w.WriteElementString("xNome", nota.NomeDestinatario[..Math.Min(60, nota.NomeDestinatario.Length)]);

        w.WriteElementString("indIEDest", "9"); // 9=não contribuinte
        if (!string.IsNullOrWhiteSpace(nota.EmailDestinatario))
            w.WriteElementString("email", nota.EmailDestinatario[..Math.Min(60, nota.EmailDestinatario.Length)]);
        w.WriteEndElement(); // dest
    }

    // Descrição obrigatória do 1º item em homologação (a SEFAZ rejeita a nota se
    // vier qualquer outra descrição no primeiro item quando tpAmb=2).
    private const string XProdHomologacao =
        "NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";

    private static void EscreverItens(XmlWriter w, NotaFiscal nota, ConfiguracaoFiscal config)
    {
        var homologacao = config.Ambiente == AmbienteFiscal.Homologacao;
        var primeiro = true;

        foreach (var item in nota.Itens)
        {
            w.WriteStartElement("det");
            w.WriteAttributeString("nItem", item.NumeroItem.ToString());

            // prod
            w.WriteStartElement("prod");
            w.WriteElementString("cProd",    item.Codigo[..Math.Min(60, item.Codigo.Length)]);
            w.WriteElementString("cEAN",     "SEM GTIN");
            // Em homologação, o 1º item leva a frase obrigatória; os demais, a descrição real.
            var xProd = homologacao && primeiro
                ? XProdHomologacao
                : item.Descricao[..Math.Min(120, item.Descricao.Length)];
            w.WriteElementString("xProd",    xProd);
            primeiro = false;
            w.WriteElementString("NCM",      ApenasDigitos(item.Ncm ?? "00000000").PadLeft(8, '0')[..8]);
            if (!string.IsNullOrWhiteSpace(item.Cest))
                w.WriteElementString("CEST", ApenasDigitos(item.Cest).PadLeft(7, '0')[..7]);
            w.WriteElementString("CFOP",     item.Cfop);
            w.WriteElementString("uCom",     item.UnidadeMedida[..Math.Min(6, item.UnidadeMedida.Length)]);
            w.WriteElementString("qCom",     FormatarQuantidade(item.Quantidade, item.Pesavel));
            w.WriteElementString("vUnCom",   FormatarDecimal(item.ValorUnitario, 10));
            w.WriteElementString("vProd",    FormatarDecimal(item.ValorTotal + item.ValorDesconto, 2));
            w.WriteElementString("cEANTrib", "SEM GTIN");
            w.WriteElementString("uTrib",    item.UnidadeMedida[..Math.Min(6, item.UnidadeMedida.Length)]);
            w.WriteElementString("qTrib",    FormatarQuantidade(item.Quantidade, item.Pesavel));
            w.WriteElementString("vUnTrib",  FormatarDecimal(item.ValorUnitario, 10));
            if (item.ValorDesconto > 0)
                w.WriteElementString("vDesc", FormatarDecimal(item.ValorDesconto, 2));
            w.WriteElementString("indTot",   "1");
            w.WriteEndElement(); // prod

            // imposto
            w.WriteStartElement("imposto");
            EscreverICMS(w, item, config);
            EscreverPIS(w, item);
            EscreverCOFINS(w, item);
            w.WriteEndElement(); // imposto

            w.WriteEndElement(); // det
        }
    }

    private static void EscreverICMS(XmlWriter w, ItemNotaFiscal item, ConfiguracaoFiscal config)
    {
        w.WriteStartElement("ICMS");

        var orig = "0"; // nacional

        if (config.Regime == RegimeTributario.SimplesNacional)
        {
            var csosn = item.CsosnIcms ?? "400";
            switch (csosn)
            {
                case "500":
                    w.WriteStartElement("ICMSSN500");
                    w.WriteElementString("orig",          orig);
                    w.WriteElementString("CSOSN",         "500");
                    w.WriteElementString("vBCSTRet",      "0.00");
                    w.WriteElementString("pST",           "0.00");
                    w.WriteElementString("vICMSSTRet",    "0.00");
                    w.WriteEndElement();
                    break;
                case "102":
                    w.WriteStartElement("ICMSSN102");
                    w.WriteElementString("orig",  orig);
                    w.WriteElementString("CSOSN", "102");
                    w.WriteEndElement();
                    break;
                default: // 400
                    w.WriteStartElement("ICMSSN400");
                    w.WriteElementString("orig",  orig);
                    w.WriteElementString("CSOSN", "400");
                    w.WriteEndElement();
                    break;
            }
        }
        else
        {
            var cst = item.CstIcms ?? "040";
            switch (cst)
            {
                case "000":
                    w.WriteStartElement("ICMS00");
                    w.WriteElementString("orig",    orig);
                    w.WriteElementString("CST",     "00");
                    w.WriteElementString("modBC",   "3");
                    w.WriteElementString("vBC",     FormatarDecimal(item.BaseIcms, 2));
                    w.WriteElementString("pICMS",   FormatarDecimal(item.AliquotaIcms, 2));
                    w.WriteElementString("vICMS",   FormatarDecimal(item.ValorIcms, 2));
                    w.WriteEndElement();
                    break;
                default: // 040 = isento
                    w.WriteStartElement("ICMS40");
                    w.WriteElementString("orig", orig);
                    w.WriteElementString("CST",  "40");
                    w.WriteEndElement();
                    break;
            }
        }

        w.WriteEndElement(); // ICMS
    }

    private static void EscreverPIS(XmlWriter w, ItemNotaFiscal item)
    {
        w.WriteStartElement("PIS");
        var cst = item.CstPisCofins ?? "07";

        if (cst is "01" or "02")
        {
            w.WriteStartElement("PISAliq");
            w.WriteElementString("CST",  cst.PadLeft(2, '0'));
            w.WriteElementString("vBC",  FormatarDecimal(item.ValorTotal, 2));
            w.WriteElementString("pPIS", FormatarDecimal(item.AliquotaPis, 2));
            w.WriteElementString("vPIS", FormatarDecimal(item.ValorPis, 2));
            w.WriteEndElement();
        }
        else
        {
            w.WriteStartElement("PISNT");
            w.WriteElementString("CST", cst.PadLeft(2, '0'));
            w.WriteEndElement();
        }

        w.WriteEndElement(); // PIS
    }

    private static void EscreverCOFINS(XmlWriter w, ItemNotaFiscal item)
    {
        w.WriteStartElement("COFINS");
        var cst = item.CstPisCofins ?? "07";

        if (cst is "01" or "02")
        {
            w.WriteStartElement("COFINSAliq");
            w.WriteElementString("CST",      cst.PadLeft(2, '0'));
            w.WriteElementString("vBC",      FormatarDecimal(item.ValorTotal, 2));
            w.WriteElementString("pCOFINS",  FormatarDecimal(item.AliquotaCofins, 2));
            w.WriteElementString("vCOFINS",  FormatarDecimal(item.ValorCofins, 2));
            w.WriteEndElement();
        }
        else
        {
            w.WriteStartElement("COFINSNT");
            w.WriteElementString("CST", cst.PadLeft(2, '0'));
            w.WriteEndElement();
        }

        w.WriteEndElement(); // COFINS
    }

    private static void EscreverTotal(XmlWriter w, NotaFiscal nota)
    {
        w.WriteStartElement("total");
        w.WriteStartElement("ICMSTot");

        w.WriteElementString("vBC",       FormatarDecimal(nota.Itens.Sum(i => i.BaseIcms), 2));
        w.WriteElementString("vICMS",     FormatarDecimal(nota.TotalIcms, 2));
        w.WriteElementString("vICMSDeson","0.00");
        w.WriteElementString("vFCP",      "0.00");
        w.WriteElementString("vBCST",     "0.00");
        w.WriteElementString("vST",       "0.00");
        w.WriteElementString("vFCPST",    "0.00");
        w.WriteElementString("vFCPSTRet", "0.00");
        w.WriteElementString("vProd",     FormatarDecimal(nota.TotalProdutos, 2));
        w.WriteElementString("vFrete",    "0.00");
        w.WriteElementString("vSeg",      "0.00");
        w.WriteElementString("vDesc",     FormatarDecimal(nota.TotalDesconto, 2));
        w.WriteElementString("vII",       "0.00");
        w.WriteElementString("vIPI",      "0.00");
        w.WriteElementString("vIPIDevol", "0.00");
        w.WriteElementString("vPIS",      FormatarDecimal(nota.TotalPis, 2));
        w.WriteElementString("vCOFINS",   FormatarDecimal(nota.TotalCofins, 2));
        w.WriteElementString("vOutro",    "0.00");
        w.WriteElementString("vNF",       FormatarDecimal(nota.TotalNota, 2));
        w.WriteElementString("vTotTrib",  FormatarDecimal(nota.TotalIcms + nota.TotalPis + nota.TotalCofins, 2));

        w.WriteEndElement(); // ICMSTot
        w.WriteEndElement(); // total
    }

    private static void EscreverTransp(XmlWriter w)
    {
        w.WriteStartElement("transp");
        w.WriteElementString("modFrete", "9"); // sem frete
        w.WriteEndElement();
    }

    private static void EscreverPag(XmlWriter w, IReadOnlyList<(string TPag, decimal VPag)> pagamentos)
    {
        w.WriteStartElement("pag");

        if (pagamentos.Count == 0)
        {
            // sem pagamento (ex.: NF de transferência)
            w.WriteStartElement("detPag");
            w.WriteElementString("tPag", "90");
            w.WriteElementString("vPag", "0.00");
            w.WriteEndElement();
        }
        else
        {
            foreach (var (tPag, vPag) in pagamentos)
            {
                w.WriteStartElement("detPag");
                w.WriteElementString("tPag", tPag.PadLeft(2, '0'));
                w.WriteElementString("vPag", FormatarDecimal(vPag, 2));
                w.WriteEndElement();
            }
        }

        w.WriteEndElement(); // pag
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Assinatura digital (RSA-SHA1, XmlDsig)
    // ─────────────────────────────────────────────────────────────────────────

    private static string Assinar(string xmlNFe, string chave44, ConfiguracaoFiscal config)
    {
        if (config.CertificadoPfxBase64 is null)
            throw new InvalidOperationException("Certificado digital não configurado.");

        var bytes = Convert.FromBase64String(config.CertificadoPfxBase64);
        var flags = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.Linux)
            ? X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet
            : X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet;

        using var cert = new X509Certificate2(bytes, config.CertificadoSenha, flags);

        var doc = new XmlDocument { PreserveWhitespace = false };
        doc.LoadXml(xmlNFe);

        var signedXml = new SignedXml(doc)
        {
            SigningKey = cert.GetRSAPrivateKey()
        };

        var reference = new Reference($"#NFe{chave44}");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        doc.DocumentElement!.AppendChild(doc.ImportNode(signedXml.GetXml(), true));

        using var ms = new MemoryStream();
        using var xw = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false
        });
        doc.WriteTo(xw);
        xw.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utilitários
    // ─────────────────────────────────────────────────────────────────────────

    private static string FormatarDecimal(decimal valor, int casas)
        => valor.ToString($"F{casas}", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatarQuantidade(decimal qtd, bool pesavel)
        => pesavel
            ? qtd.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
            : ((long)qtd).ToString();

    private static string ApenasDigitos(string s)
        => new(s.Where(char.IsDigit).ToArray());

    private static int CalcularDV(string chave43)
    {
        // módulo 11, pesos 2-9 da direita para esquerda
        int soma = 0, peso = 2;
        for (int i = chave43.Length - 1; i >= 0; i--)
        {
            soma += (chave43[i] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }
        var rem = soma % 11;
        return rem < 2 ? 0 : 11 - rem;
    }

    private static int UfParaCodigo(string uf) => uf.ToUpper() switch
    {
        "AC" => 12, "AL" => 27, "AP" => 16, "AM" => 13, "BA" => 29,
        "CE" => 23, "DF" => 53, "ES" => 32, "GO" => 52, "MA" => 21,
        "MT" => 51, "MS" => 50, "MG" => 31, "PA" => 15, "PB" => 25,
        "PR" => 41, "PE" => 26, "PI" => 22, "RJ" => 33, "RN" => 24,
        "RS" => 43, "RO" => 11, "RR" => 14, "SC" => 42, "SP" => 35,
        "SE" => 28, "TO" => 17, _   => 35
    };
}
