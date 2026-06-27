using Microsoft.Extensions.Logging;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Implementação stub do serviço de Distribuição DFe.
/// Para ativar: instalar DFe.NET (NFe.NET), carregar certificado A1 da empresa
/// e substituir os métodos pelas chamadas reais ao webservice SEFAZ.
/// Webservice: https://www.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx
/// </summary>
public class DistribuicaoDFeService(ILogger<DistribuicaoDFeService> logger) : IDistribuicaoDFeService
{
    public Task<ResultadoConsultaDFe> ConsultarAsync(
        string cnpj, string uf, string ultimoNSU, CancellationToken ct)
    {
        // TODO: integração real com DFe.NET
        // var config = new ConfiguracaoServico { ... }
        // var certificado = X509CertificateLoader.LoadPkcs12(bytes, senha)
        // var servico = new NFeDistribuicaoDFe(config, certificado)
        // var resultado = servico.ConsultarDistribuicaoDFePorNSU(cnpj, ultimoNSU)

        logger.LogWarning(
            "DistribuicaoDFeService: modo stub. Configure o certificado A1 para consulta real na SEFAZ.");

        return Task.FromResult(new ResultadoConsultaDFe(
            Sucesso: false,
            Erro: "Certificado digital A1 não configurado. Acesse Configurações → Fiscal para cadastrar o certificado.",
            UltimoNSU: ultimoNSU,
            Documentos: []));
    }

    public Task<bool> ManifestarAsync(
        string cnpj, string uf, string chaveAcesso,
        ManifestacaoTipo tipo, string? justificativa, CancellationToken ct)
    {
        // TODO: chamada real ao NFeAutorizacao (evento de manifestação)
        // Código do evento varia por tipo: 210200=Confirmação, 210210=Ciência, etc.
        logger.LogWarning(
            "DistribuicaoDFeService: manifestação em modo stub para chave {Chave}.", chaveAcesso);

        return Task.FromResult(false);
    }

    public Task<string?> BaixarXmlAsync(
        string cnpj, string uf, string chaveAcesso, CancellationToken ct)
    {
        logger.LogWarning(
            "DistribuicaoDFeService: download XML em modo stub para chave {Chave}.", chaveAcesso);

        return Task.FromResult<string?>(null);
    }
}
