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

    public void IrParaProducao() => Ambiente = AmbienteFiscal.Producao;
    public void IrParaHomologacao() => Ambiente = AmbienteFiscal.Homologacao;
}

public enum RegimeTributario { SimplesNacional = 1, LucroPresumido = 2, LucroReal = 3 }
public enum AmbienteFiscal { Producao = 1, Homologacao = 2 }
