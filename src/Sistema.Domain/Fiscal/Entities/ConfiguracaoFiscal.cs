using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Fiscal.Entities;

/// <summary>Configuração fiscal da empresa (regime tributário, séries, ambiente).</summary>
public class ConfiguracaoFiscal : Entity
{
    public Guid EmpresaId { get; private set; }
    public RegimeTributario Regime { get; private set; }
    public AmbienteFiscal Ambiente { get; private set; }  // Producao | Homologacao
    public int SerieNFe { get; private set; } = 1;
    public int SerieNFCe { get; private set; } = 1;
    public long ProximoNumerNFe { get; private set; } = 1;
    public long ProximoNumerNFCe { get; private set; } = 1;
    public string? CscIdNFCe { get; private set; }         // ID do CSC (Código de Segurança do Contribuinte)
    public string? CscTokenNFCe { get; private set; }      // Token CSC para NFC-e
    public string? CaminhoXmlNFe { get; private set; }     // Pasta local para salvar XMLs
    public string? EmailContador { get; private set; }
    public bool EnviarEmailAposEmissao { get; private set; }
    public string? CertificadoPfxBase64 { get; set; }
    public string? CertificadoSenha { get; set; }

    // Controle de paginação DFe — salvo após cada consulta bem-sucedida ao SEFAZ
    public string UltimoNsuDFe { get; private set; } = "0";
    public string UltimoNsuCteDFe { get; private set; } = "0";

    // ── Parâmetros gerais de documentos ──────────────────────────────────
    public string? NaturezaOperacaoPadrao { get; private set; }
    public string? ContingenciaPadrao { get; private set; }        // SVC_AN | SVC_RS | Offline
    public string? FormatoDanfe { get; private set; }              // Retrato | Paisagem
    public string? TipoImpressaoNFCe { get; private set; }         // Termica80 | Termica58 | A4
    public bool ImprimirAutomaticamenteNFCe { get; private set; }
    // Emissão automática de NFC-e na venda. Desligue quando o CNPJ ainda não é
    // válido (ex.: filial em processo de abertura) para vender sem tentar emitir.
    public bool EmissaoNFCeAtiva { get; private set; } = true;

    public void DefinirEmissaoNFCe(bool ativa) => EmissaoNFCeAtiva = ativa;

    // ── Tributação padrão de produtos ────────────────────────────────────
    public string? CsosnPadrao { get; private set; }
    public string? CstIcmsPadrao { get; private set; }
    public decimal AliquotaIcmsPadrao { get; private set; }
    public decimal AliquotaIcmsInterestadual { get; private set; }
    public string? OrigemPadrao { get; private set; }
    public string? CstPisPadrao { get; private set; }
    public decimal AliquotaPisPadrao { get; private set; }
    public string? CstCofinsPadrao { get; private set; }
    public decimal AliquotaCofinsPadrao { get; private set; }
    public string? CfopVendaEstadual { get; private set; }
    public string? CfopVendaInterestadual { get; private set; }
    public string? CfopVendaConsumidor { get; private set; }

    // ── NFS-e (serviços municipais) ──────────────────────────────────────
    public bool HabilitarNFSe { get; private set; }
    public string? InscricaoMunicipal { get; private set; }
    public string? CodigoMunicipioIbge { get; private set; }
    public int SerieNFSe { get; private set; } = 1;
    public string? RegimeEspecialTributacao { get; private set; }
    public string? CodigoServicoMunicipalPadrao { get; private set; }
    public decimal AliquotaIssPadrao { get; private set; }
    public bool IssRetidoFonte { get; private set; }
    public bool IncentivadorCultural { get; private set; }

    // ── MDF-e (manifesto de transporte) ──────────────────────────────────
    public bool HabilitarMDFe { get; private set; }
    public int SerieMDFe { get; private set; } = 1;
    public long ProximoNumeroMDFe { get; private set; } = 1;
    public string? TipoEmitenteMDFe { get; private set; }
    public string? ModalTransporteMDFe { get; private set; }
    public string? Rntrc { get; private set; }

    private ConfiguracaoFiscal() { }

    public static ConfiguracaoFiscal Criar(Guid empresaId, RegimeTributario regime,
        AmbienteFiscal ambiente = AmbienteFiscal.Homologacao)
        => new()
        {
            EmpresaId = empresaId,
            Regime = regime,
            Ambiente = ambiente
        };

    public long AvancarNumeracaoNFe() => ProximoNumerNFe++;
    public long AvancarNumeracaoNFCe() => ProximoNumerNFCe++;

    public void ConfigurarNFCe(string cscId, string cscToken)
    {
        CscIdNFCe = cscId;
        CscTokenNFCe = cscToken;
    }

    public void AtualizarRegime(RegimeTributario regime) => Regime = regime;
    public void IrParaProducao() => Ambiente = AmbienteFiscal.Producao;
    public void IrParaHomologacao() => Ambiente = AmbienteFiscal.Homologacao;

    /// <summary>Define as séries e o próximo número de NF-e/NFC-e (usado na tela de Configurações).</summary>
    public void DefinirSeriesENumeracao(int? serieNFe, int? serieNFCe, long? proximoNFe, long? proximoNFCe)
    {
        if (serieNFe is > 0) SerieNFe = serieNFe.Value;
        if (serieNFCe is > 0) SerieNFCe = serieNFCe.Value;
        if (proximoNFe is > 0) ProximoNumerNFe = proximoNFe.Value;
        if (proximoNFCe is > 0) ProximoNumerNFCe = proximoNFCe.Value;
    }

    /// <summary>Define os dados de envio de e-mail do contador/cópia fixa.</summary>
    public void DefinirEmail(string? emailContador, bool enviarAposEmissao)
    {
        EmailContador = emailContador;
        EnviarEmailAposEmissao = enviarAposEmissao;
    }

    /// <summary>Parâmetros gerais de documentos (natureza, contingência, DANFE, impressão NFC-e).</summary>
    public void DefinirParametrosDocumentos(string? naturezaOperacaoPadrao, string? contingenciaPadrao,
        string? formatoDanfe, string? tipoImpressaoNFCe, bool imprimirAutomaticamenteNFCe)
    {
        NaturezaOperacaoPadrao = naturezaOperacaoPadrao;
        ContingenciaPadrao = contingenciaPadrao;
        FormatoDanfe = formatoDanfe;
        TipoImpressaoNFCe = tipoImpressaoNFCe;
        ImprimirAutomaticamenteNFCe = imprimirAutomaticamenteNFCe;
    }

    /// <summary>Tributação padrão aplicada a novos produtos (ICMS/PIS/COFINS/CFOP).</summary>
    public void DefinirTributacaoPadrao(
        string? csosn, string? cstIcms, decimal aliqIcms, decimal aliqIcmsInter, string? origem,
        string? cstPis, decimal aliqPis, string? cstCofins, decimal aliqCofins,
        string? cfopEstadual, string? cfopInterestadual, string? cfopConsumidor)
    {
        CsosnPadrao = csosn;
        CstIcmsPadrao = cstIcms;
        AliquotaIcmsPadrao = aliqIcms;
        AliquotaIcmsInterestadual = aliqIcmsInter;
        OrigemPadrao = origem;
        CstPisPadrao = cstPis;
        AliquotaPisPadrao = aliqPis;
        CstCofinsPadrao = cstCofins;
        AliquotaCofinsPadrao = aliqCofins;
        CfopVendaEstadual = cfopEstadual;
        CfopVendaInterestadual = cfopInterestadual;
        CfopVendaConsumidor = cfopConsumidor;
    }

    /// <summary>Configuração de NFS-e (serviços municipais).</summary>
    public void DefinirNFSe(bool habilitar, string? inscricaoMunicipal, string? codigoMunicipioIbge,
        int serie, string? regimeEspecial, string? codigoServico, decimal aliquotaIss,
        bool issRetido, bool incentivadorCultural)
    {
        HabilitarNFSe = habilitar;
        InscricaoMunicipal = inscricaoMunicipal;
        CodigoMunicipioIbge = codigoMunicipioIbge;
        if (serie > 0) SerieNFSe = serie;
        RegimeEspecialTributacao = regimeEspecial;
        CodigoServicoMunicipalPadrao = codigoServico;
        AliquotaIssPadrao = aliquotaIss;
        IssRetidoFonte = issRetido;
        IncentivadorCultural = incentivadorCultural;
    }

    /// <summary>Configuração de MDF-e (manifesto de documentos fiscais).</summary>
    public void DefinirMDFe(bool habilitar, int serie, long proximoNumero,
        string? tipoEmitente, string? modalTransporte, string? rntrc)
    {
        HabilitarMDFe = habilitar;
        if (serie > 0) SerieMDFe = serie;
        if (proximoNumero > 0) ProximoNumeroMDFe = proximoNumero;
        TipoEmitenteMDFe = tipoEmitente;
        ModalTransporteMDFe = modalTransporte;
        Rntrc = rntrc;
    }

    /// <summary>
    /// Salva o último NSU retornado pelo SEFAZ. Garante progressão numérica correta
    /// (compara como long, não como string, para evitar "9" > "10").
    /// </summary>
    public void AvancarNsuDFe(string nsuSefaz)
    {
        if (long.TryParse(nsuSefaz, out var novo) &&
            long.TryParse(UltimoNsuDFe, out var atual) &&
            novo > atual)
            UltimoNsuDFe = nsuSefaz;
    }

    public void AvancarNsuCteDFe(string nsuSefaz)
    {
        if (long.TryParse(nsuSefaz, out var novo) &&
            long.TryParse(UltimoNsuCteDFe, out var atual) &&
            novo > atual)
            UltimoNsuCteDFe = nsuSefaz;
    }
}

public enum RegimeTributario { SimplesNacional = 1, LucroPresumido = 2, LucroReal = 3 }
public enum AmbienteFiscal { Producao = 1, Homologacao = 2 }
