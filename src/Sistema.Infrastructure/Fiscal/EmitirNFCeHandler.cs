using MediatR;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Vendas.Events;
using Sistema.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Emite NFC-e automaticamente ao receber VendaFinalizadaEvent.
/// Stub completo: gera a entidade, QR Code e registra status pendente.
/// Transmissão real à SEFAZ ocorre via DFe.NET quando certificado A1 estiver configurado.
/// </summary>
public class EmitirNFCeHandler(SistemaDbContext db) : INotificationHandler<VendaFinalizadaEvent>
{
    // URLs de consulta QR Code por UF (NT 2013.001 rev. 9 — produção)
    private static readonly Dictionary<string, string> UrlConsultaPorUf = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = "https://www.sefaznet.ac.gov.br/nfce/consulta",
        ["AL"] = "https://nfce.sefaz.al.gov.br/consultanfce",
        ["AM"] = "https://sistemas.sefaz.am.gov.br/nfceweb/consultaNFCe.html",
        ["AP"] = "https://www.sefaz.ap.gov.br/nfce/nfcep.php",
        ["BA"] = "https://www.sefaz.ba.gov.br/codigobarras/nfce/index.aspx",
        ["CE"] = "https://nfcee.sefaz.ce.gov.br/pages/consultaNFCe.html",
        ["DF"] = "https://www.fazenda.df.gov.br/nfce/consulta",
        ["ES"] = "https://app.sefaz.es.gov.br/ConsultaNFCe",
        ["GO"] = "https://nfce.sefaz.go.gov.br/pages/consultaNFCe.jsf",
        ["MA"] = "https://www.nfce.sefaz.ma.gov.br/portal/consultaNFCe.do",
        ["MG"] = "https://nfce.fazenda.mg.gov.br/portalnfce",
        ["MS"] = "https://www.dfe.ms.gov.br/dfe-portal/faces/pages_sistema/nfce-portal-externo/nfce-consulta-chave-externo.xhtml",
        ["MT"] = "https://www.sefaz.mt.gov.br/nfce/consultanfce",
        ["PA"] = "https://appnfc.sefa.pa.gov.br/portal/view/consultas/nfce/nfceDadosEmitente.seam",
        ["PB"] = "https://www.receita.pb.gov.br/nfce",
        ["PE"] = "https://nfce.sefaz.pe.gov.br/nfce-web/consultarNFCe",
        ["PI"] = "https://www.sefaz.pi.gov.br/nfce/consulta",
        ["PR"] = "https://www.fazenda.pr.gov.br/nfce/consulta",
        ["RJ"] = "https://nfce.fazenda.rj.gov.br/consulta",
        ["RN"] = "https://nfce.set.rn.gov.br/consultarNFCe",
        ["RO"] = "https://www.nfce.sefin.ro.gov.br",
        ["RR"] = "https://www.sefaz.rr.gov.br/nfce/servlet/wp.consulta",
        ["RS"] = "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx",
        ["SC"] = "https://sat.sef.sc.gov.br/nfce/consulta",
        ["SE"] = "https://nfce.se.gov.br/consultaNFCe",
        ["SP"] = "https://www.nfce.fazenda.sp.gov.br/NFCeConsultaPublica",
        ["TO"] = "https://apps.sefaz.to.gov.br/Portal/portalNFCe",
    };

    private static readonly Dictionary<string, string> UrlHomologacaoPorUf = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = "https://homologacao.sefaznet.ac.gov.br/nfce/consulta",
        ["AM"] = "https://homnfce.sefaz.am.gov.br/nfceweb/consultaNFCe.html",
        ["BA"] = "https://hnfce.sefaz.ba.gov.br/codigobarras/nfce/index.aspx",
        ["CE"] = "https://hnfcee.sefaz.ce.gov.br/pages/consultaNFCe.html",
        ["GO"] = "https://homolog.sefaz.go.gov.br/nfce/pages/consultaNFCe.jsf",
        ["MG"] = "https://hnfce.fazenda.mg.gov.br/portalnfce",
        ["MS"] = "https://homologacao.dfe.ms.gov.br/dfe-portal/faces/pages_sistema/nfce-portal-externo/nfce-consulta-chave-externo.xhtml",
        ["MT"] = "https://homologacao.sefaz.mt.gov.br/nfce/consultanfce",
        ["PE"] = "https://hom.nfce.sefaz.pe.gov.br/nfce-web/consultarNFCe",
        ["PR"] = "https://homologacao.fazenda.pr.gov.br/nfce/consulta",
        ["RS"] = "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx",
        ["SC"] = "https://hom.sat.sef.sc.gov.br/nfce/consulta",
        ["SP"] = "https://homologacao.nfce.fazenda.sp.gov.br/NFCeConsultaPublica",
    };

    public async Task Handle(VendaFinalizadaEvent evt, CancellationToken ct)
    {
        // Carregar config fiscal e empresa
        var config = await db.ConfiguracoesFiscais
            .FirstOrDefaultAsync(c => c.EmpresaId == evt.EmpresaId, ct);
        if (config is null) return;

        var empresa = await db.Empresas
            .FirstOrDefaultAsync(e => e.Id == evt.EmpresaId, ct);
        if (empresa is null) return;

        // Buscar dados dos produtos para montar itens da NFC-e
        var produtoIds = evt.Itens.Select(i => i.ProdutoId).ToList();
        var produtos = await db.Produtos
            .Where(p => produtoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // Criar NFC-e
        var numero = config.AvancarNumeracaoNFCe();
        var nfce = NotaFiscal.Criar(
            evt.EmpresaId,
            ModeloNF.NFCe,
            config.SerieNFCe,
            numero,
            NaturezaOperacao.VendaConsumidor,
            clienteId: evt.ClienteId,
            vendaId: evt.VendaId);

        if (!string.IsNullOrWhiteSpace(evt.CpfCnpjConsumidor))
            nfce.DefinirCpfCnpjConsumidor(evt.CpfCnpjConsumidor);

        // Montar itens
        int numItem = 0;
        bool isSimples = empresa.RegimeTributario == "SN";

        foreach (var itemVenda in evt.Itens)
        {
            numItem++;
            produtos.TryGetValue(itemVenda.ProdutoId, out var produto);

            var cfop = empresa.Uf == "SP" ? "5102" : "5102"; // venda dentro do estado
            var item = ItemNotaFiscal.Criar(
                nfce.Id, numItem,
                codigo: produto?.CodigoBarras ?? produto?.Codigo ?? numItem.ToString(),
                descricao: itemVenda.Descricao,
                cfop: cfop,
                unidade: "UN",
                quantidade: itemVenda.Quantidade,
                valorUnitario: itemVenda.PrecoUnitario,
                valorDesconto: itemVenda.TotalDesconto,
                ncm: produto?.Ncm,
                cest: produto?.Cest,
                produtoId: itemVenda.ProdutoId);

            if (produto is not null)
            {
                item.CalcularImpostos(
                    cstIcms: isSimples ? null : produto.CstIcms,
                    csosnIcms: isSimples ? (produto.CsosnIcms ?? "400") : null,
                    aliqIcms: isSimples ? 0 : produto.AliquotaIcms,
                    cstPis: produto.CstPisCofins ?? "07",
                    aliqPis: produto.AliquotaPis,
                    aliqCofins: produto.AliquotaCofins);
            }

            nfce.AdicionarItem(item);
        }

        // Gerar QR Code (NT 2013.001 rev. 9 — sem transmissão, chave temporária)
        var chaveTemp = GerarChaveTemp(empresa.Cnpj, config.SerieNFCe, numero, config.Ambiente);
        nfce.RegistrarTransmissao(chaveTemp, ""); // xml será gerado pelo DFe.NET

        var uf = empresa.Uf.ToUpper();
        var ambienteId = config.Ambiente == AmbienteFiscal.Producao ? 1 : 2;
        var urlBase = (config.Ambiente == AmbienteFiscal.Producao
            ? UrlConsultaPorUf
            : UrlHomologacaoPorUf)
            .GetValueOrDefault(uf, UrlConsultaPorUf.GetValueOrDefault("SP", "https://www.nfce.fazenda.sp.gov.br/NFCeConsultaPublica")!);

        var qrCodeData = GerarQrCodeData(chaveTemp, ambienteId, config.CscIdNFCe, config.CscTokenNFCe);
        var urlConsulta = $"{urlBase}?p={qrCodeData}";
        nfce.RegistrarQrCode(qrCodeData, urlConsulta);

        db.NotasFiscais.Add(nfce);

        // Vincular nota fiscal à venda
        var venda = await db.Vendas.FirstOrDefaultAsync(v => v.Id == evt.VendaId, ct);
        venda?.VincularNotaFiscal(nfce.Id);

        // SaveChanges é feito pelo contexto externo (VendaFinalizadaHandler já chama uow.SalvarAsync)
        // mas como este handler é chamado APÓS o save via MediatR Publish pós-save,
        // precisamos salvar aqui.
        await db.SaveChangesAsync(ct);
    }

    // Gera chave de acesso de 44 dígitos conforme leiaute SEFAZ
    private static string GerarChaveTemp(string cnpj, int serie, long numero, AmbienteFiscal ambiente)
    {
        var now = DateTime.Now;
        // CNPJ alfanumérico (IN RFB 2.229/2024): remove pontuação mas preserva letras A–Z
        var cnpjNum = cnpj.Replace(".", "").Replace("/", "").Replace("-", "").ToUpperInvariant();
        var cNF = Random.Shared.Next(10000000, 99999999).ToString();
        // cMF=65 (NFC-e), cEmi=1 (emissão normal)
        var semDv = $"35{now:yyMM}{cnpjNum}65{serie:D3}{now:yyMM}{numero:D9}1{cNF}";
        var dv = CalcularDv(semDv);
        return semDv + dv;
    }

    private static int CalcularDv(string chave)
    {
        int peso = 2, soma = 0;
        for (int i = chave.Length - 1; i >= 0; i--)
        {
            soma += int.Parse(chave[i].ToString()) * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    // Monta o parâmetro p= do QR Code conforme NT 2013.001
    // Formato: chave|versao|ambiente|csc_id|digest_sha1_hex
    private static string GerarQrCodeData(string chave, int ambiente, string? cscId, string? cscToken)
    {
        var versao = "2";
        var idCsc = cscId ?? "000001";
        var token = cscToken ?? string.Empty;

        // Hash SHA-1 de (chave + "|" + versao + "|" + ambiente + "|" + idCsc + token)
        var payload = $"{chave}|{versao}|{ambiente}|{idCsc}{token}";
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(payload))).ToUpper();

        return $"{chave}|{versao}|{ambiente}|{idCsc}|{hash}";
    }
}
