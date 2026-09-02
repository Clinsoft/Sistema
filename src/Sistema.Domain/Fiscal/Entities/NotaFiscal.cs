using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Fiscal.Entities;

/// <summary>NF-e (modelo 55) ou NFC-e (modelo 65).</summary>
public class NotaFiscal : Entity
{
    public Guid EmpresaId { get; private set; }
    public ModeloNF Modelo { get; private set; }
    public int Serie { get; private set; }
    public long Numero { get; private set; }
    public string? ChaveAcesso { get; private set; }
    public string? Protocolo { get; private set; }
    public StatusNF Status { get; private set; }
    public NaturezaOperacao NaturezaOperacao { get; private set; }
    public DateTime DataEmissao { get; private set; }
    public DateTime? DataSaida { get; private set; }

    // Destinatário
    public Guid? ClienteId { get; private set; }
    public string? CpfCnpjDestinatario { get; private set; }
    public string? NomeDestinatario { get; private set; }
    public string? EmailDestinatario { get; private set; }

    // NFC-e: consumidor pode informar CPF (PF) ou CNPJ alfanumérico (PJ) — NT 2013.001
    // CNPJ Alfanumérico (IN RFB 2.229/2024): raiz (pos. 1–8) aceita A–Z + 0–9 a partir de jul/2026
    public string? CpfCnpjConsumidor { get; private set; }

    // Totais — tributos atuais
    public decimal TotalProdutos { get; private set; }
    public decimal TotalDesconto { get; private set; }
    public decimal TotalIcms { get; private set; }
    public decimal TotalPis { get; private set; }
    public decimal TotalCofins { get; private set; }
    public decimal TotalNota { get; private set; }

    // ── REFORMA TRIBUTÁRIA (EC 132/2023) ──────────────────────────────
    // Zerados até SEFAZ publicar NT do novo leiaute XML (previsto 2026).
    public decimal TotalIbs { get; private set; }
    public decimal TotalCbs { get; private set; }
    public decimal TotalIs { get; private set; }
    public decimal TotalSplitPayment { get; private set; }

    // XML e PDF
    public string? XmlEnvio { get; private set; }
    public string? XmlRetorno { get; private set; }
    public string? MotivoRejeicao { get; private set; }
    public string? ChaveCartaCorrecao { get; private set; }

    // NFC-e: QR Code e URL de consulta (obrigatórios pela NT 2013.001 rev. 9)
    public string? QrCode { get; private set; }
    public string? UrlConsultaQrCode { get; private set; }

    // Venda de origem
    public Guid? VendaId { get; private set; }

    private readonly List<ItemNotaFiscal> _itens = [];
    public IReadOnlyList<ItemNotaFiscal> Itens => _itens.AsReadOnly();

    private NotaFiscal() { }

    public static NotaFiscal Criar(Guid empresaId, ModeloNF modelo, int serie, long numero,
        NaturezaOperacao natureza, Guid? clienteId = null, Guid? vendaId = null)
        => new()
        {
            EmpresaId = empresaId, Modelo = modelo, Serie = serie, Numero = numero,
            NaturezaOperacao = natureza, Status = StatusNF.EmDigitacao,
            DataEmissao = DateTime.Now, ClienteId = clienteId, VendaId = vendaId
        };

    public void AdicionarItem(ItemNotaFiscal item)
    {
        _itens.Add(item);
        RecalcularTotais();
    }

    public void DefinirDestinatario(string cpfCnpj, string nome, string? email = null)
    {
        CpfCnpjDestinatario = cpfCnpj;
        NomeDestinatario = nome;
        EmailDestinatario = email;
    }

    /// <param name="cpfOuCnpj">Somente dígitos/letras (sem máscara). CPF=11 chars, CNPJ=14 chars alfanum.</param>
    public void DefinirCpfCnpjConsumidor(string cpfOuCnpj) => CpfCnpjConsumidor = cpfOuCnpj.ToUpperInvariant();

    public void RegistrarQrCode(string qrCode, string urlConsulta)
    {
        QrCode = qrCode;
        UrlConsultaQrCode = urlConsulta;
    }

    public void RegistrarTransmissao(string chave, string xml)
    {
        ChaveAcesso = chave;
        XmlEnvio = xml;
        Status = StatusNF.Transmitindo;
    }

    public void RegistrarAutorizacao(string protocolo, string xmlRetorno)
    {
        Protocolo = protocolo;
        XmlRetorno = xmlRetorno;
        Status = StatusNF.Autorizada;
        DataSaida ??= DateTime.Now;
    }

    public void RegistrarRejeicao(string motivo, string xmlRetorno)
    {
        MotivoRejeicao = motivo;
        XmlRetorno = xmlRetorno;
        Status = StatusNF.Rejeitada;
    }

    public string? ProtocoloCancelamento { get; private set; }
    public string? JustificativaCancelamento { get; private set; }
    public DateTime? DataCancelamento { get; private set; }

    /// <summary>Cancelamento efetivado na SEFAZ (evento 110111): mantém o protocolo de
    /// autorização e registra o protocolo/justificativa do cancelamento.</summary>
    public void Cancelar(string protocoloCancelamento, string justificativa)
    {
        Status = StatusNF.Cancelada;
        ProtocoloCancelamento = protocoloCancelamento;
        JustificativaCancelamento = justificativa;
        DataCancelamento = DateTime.UtcNow;
    }

    public void Inutilizar(string justificativa)
    {
        Status = StatusNF.Inutilizada;
        MotivoRejeicao = justificativa;
    }

    public void RegistrarCartaCorrecao(string chave) => ChaveCartaCorrecao = chave;

    private void RecalcularTotais()
    {
        // Soma os produtos JÁ ARREDONDADOS por item (2 casas) — no XML cada <vProd>
        // vai arredondado, então o total precisa bater com a soma deles (senão cStat 564).
        TotalProdutos = _itens.Sum(i => Math.Round(i.Quantidade * i.ValorUnitario, 2, MidpointRounding.AwayFromZero));
        TotalDesconto = _itens.Sum(i => i.ValorDesconto);
        TotalIcms = _itens.Sum(i => i.ValorIcms);
        TotalPis = _itens.Sum(i => i.ValorPis);
        TotalCofins = _itens.Sum(i => i.ValorCofins);
        TotalNota = TotalProdutos - TotalDesconto;

        // Reforma: recalcula quando os campos passarem a ser preenchidos
        TotalIbs = _itens.Sum(i => i.ValorIbs);
        TotalCbs = _itens.Sum(i => i.ValorCbs);
        TotalIs = _itens.Sum(i => i.ValorIs);
        TotalSplitPayment = _itens.Sum(i => i.ValorRetidoSplitPayment);
    }
}

public enum ModeloNF { NFe = 55, NFCe = 65 }
public enum StatusNF { EmDigitacao, Transmitindo, Autorizada, Rejeitada, Cancelada, Inutilizada, DenegadaSefaz }
public enum NaturezaOperacao { VendaProduto = 1, VendaConsumidor = 2, Devolucao = 3, Transferencia = 4, Remessa = 5 }
