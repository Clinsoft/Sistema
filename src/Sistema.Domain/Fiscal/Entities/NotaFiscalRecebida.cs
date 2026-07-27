using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Fiscal.Entities;

/// <summary>
/// NF-e recebida via Distribuição DFe (nfeDistDFeInt SEFAZ).
/// Representa documentos emitidos por terceiros contra o CNPJ da empresa.
/// </summary>
public class NotaFiscalRecebida : Entity
{
    public Guid EmpresaId { get; private set; }

    // Identificação do documento
    public string ChaveAcesso { get; private set; } = null!;  // 44 dígitos
    public string NSU { get; private set; } = null!;          // Número Sequencial Único SEFAZ
    public string Modelo { get; private set; } = null!;       // "55" = NF-e, "65" = NFC-e
    public string Serie { get; private set; } = null!;
    public long Numero { get; private set; }
    public DateTime DataEmissao { get; private set; }

    // Emitente
    public string EmitenteCnpj { get; private set; } = null!;
    public string EmitenteNome { get; private set; } = null!;
    public string? EmitenteUF { get; private set; }

    // Valores
    public decimal ValorTotal { get; private set; }

    // Situação na SEFAZ
    public SituacaoNFeRecebida Situacao { get; private set; }

    // Manifestação do destinatário
    public ManifestacaoTipo? Manifestacao { get; private set; }
    public DateTime? DataManifestacao { get; private set; }
    public string? JustificativaManifestacao { get; private set; }

    // XML completo (baixado após ciência/confirmação)
    public string? XmlNota { get; private set; }

    /// <summary>CT-e: chaves das NF-e transportadas, separadas por vírgula.</summary>
    public string? ChavesReferenciadas { get; private set; }

    public DateTime DataConsulta { get; private set; }

    private NotaFiscalRecebida() { }

    public static NotaFiscalRecebida Criar(
        Guid empresaId, string chave, string nsu, string modelo, string serie, long numero,
        DateTime dataEmissao, string emitenteCnpj, string emitenteNome, string? emitenteUF,
        decimal valorTotal, SituacaoNFeRecebida situacao)
        => new()
        {
            EmpresaId = empresaId,
            ChaveAcesso = chave,
            NSU = nsu,
            Modelo = modelo,
            Serie = serie,
            Numero = numero,
            DataEmissao = dataEmissao,
            EmitenteCnpj = emitenteCnpj,
            EmitenteNome = emitenteNome,
            EmitenteUF = emitenteUF,
            ValorTotal = valorTotal,
            Situacao = situacao,
            DataConsulta = DateTime.UtcNow,
        };

    public void Manifestar(ManifestacaoTipo tipo, string? justificativa = null)
    {
        Manifestacao = tipo;
        DataManifestacao = DateTime.UtcNow;
        JustificativaManifestacao = justificativa;
    }

    public void AtualizarSituacao(SituacaoNFeRecebida situacao) => Situacao = situacao;

    public void SalvarXml(string xml) => XmlNota = xml;
    public void DefinirChavesReferenciadas(IEnumerable<string> chaves)
        => ChavesReferenciadas = string.Join(",", chaves);

    public void AtualizarDadosConsulta(SituacaoNFeRecebida situacao)
    {
        Situacao = situacao;
        DataConsulta = DateTime.UtcNow;
    }
}

public enum SituacaoNFeRecebida
{
    Autorizada = 100,
    Cancelada = 101,
    Denegada = 110,
}

public enum ManifestacaoTipo
{
    CienciaOperacao = 210210,
    ConfirmacaoOperacao = 210200,
    DesconhecimentoOperacao = 210220,
    OperacaoNaoRealizada = 210240,
}
