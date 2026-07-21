using MediatR;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Events;
using Sistema.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Emite a NFC-e ao finalizar a venda: monta e assina o XML, transmite à SEFAZ
/// (ambiente conforme a Configuração Fiscal) e registra autorização ou rejeição.
/// A transmissão é protegida — uma falha não derruba a venda.
/// </summary>
public class EmitirNFCeHandler(SistemaDbContext db, INFeTransmissaoService transmissao)
    : INotificationHandler<VendaFinalizadaEvent>
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
        // Venda no crediário NÃO gera cupom fiscal (NFC-e) — apenas o cupom
        // simples de venda é impresso pelo caixa.
        if (evt.Pagamentos.Any(p => p.Forma == FormaPagamento.Crediario))
            return;

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

        // Chave de acesso (44 díg.) calculada UMA vez e reaproveitada no QR e no XML.
        var chave44 = NFeXmlBuilder.CalcularChaveAcesso(nfce, empresa, config);

        // QR Code (NT 2013.001) com a chave real e o CSC da NFC-e
        var uf = empresa.Uf.ToUpper();
        var ambienteId = config.Ambiente == AmbienteFiscal.Producao ? 1 : 2;
        var urlBase = (config.Ambiente == AmbienteFiscal.Producao ? UrlConsultaPorUf : UrlHomologacaoPorUf)
            .GetValueOrDefault(uf, UrlConsultaPorUf.GetValueOrDefault("SP", "https://www.nfce.fazenda.sp.gov.br/NFCeConsultaPublica")!);
        var qrCodeData = GerarQrCodeData(chave44, ambienteId, config.CscIdNFCe, config.CscTokenNFCe);
        nfce.RegistrarQrCode(qrCodeData, $"{urlBase}?p={qrCodeData}");

        // Monta, assina e TRANSMITE à SEFAZ. Uma falha de transmissão (SEFAZ fora,
        // rejeição, sem certificado) NÃO pode derrubar a venda — a nota fica salva
        // com o status correspondente e pode ser retransmitida depois.
        try
        {
            var pagamentos = evt.Pagamentos
                .Select(p => (TPag: MapearTPag(p.Forma), VPag: p.Valor))
                .ToList();
            var xmlAssinado = NFeXmlBuilder.GerarXmlAssinadoComChave(nfce, empresa, config, chave44, pagamentos);
            nfce.RegistrarTransmissao(chave44, xmlAssinado);

            var resultado = await transmissao.TransmitirAsync(xmlAssinado, config, ct);
            if (resultado.Autorizada)
                nfce.RegistrarAutorizacao(resultado.Protocolo!, resultado.XmlRetorno ?? xmlAssinado);
            else
                nfce.RegistrarRejeicao(resultado.MotivoRejeicao ?? "Rejeitada pela SEFAZ",
                    resultado.XmlRetorno ?? "");
        }
        catch (Exception ex)
        {
            // Sem certificado / SEFAZ indisponível: mantém a nota pendente para retransmitir.
            nfce.RegistrarRejeicao($"Falha na transmissão: {ex.Message}", "");
        }

        db.NotasFiscais.Add(nfce);

        // Vincular nota fiscal à venda
        var venda = await db.Vendas.FirstOrDefaultAsync(v => v.Id == evt.VendaId, ct);
        venda?.VincularNotaFiscal(nfce.Id);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Forma de pagamento interna → tPag da SEFAZ (Anexo I do MOC).</summary>
    private static string MapearTPag(FormaPagamento forma) => forma switch
    {
        FormaPagamento.Dinheiro      => "01",
        FormaPagamento.Cheque        => "02",
        FormaPagamento.CartaoCredito => "03",
        FormaPagamento.CartaoDebito  => "04",
        FormaPagamento.Crediario     => "05",  // crédito loja
        FormaPagamento.Vale          => "10",  // vale alimentação/refeição (aprox.)
        FormaPagamento.Boleto        => "15",  // boleto bancário
        FormaPagamento.Pix           => "17",  // pagamento instantâneo (Pix)
        _                            => "99",  // outros
    };

    // A chave de acesso é calculada por NFeXmlBuilder.CalcularChaveAcesso (única fonte).

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
