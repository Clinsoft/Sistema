using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Leitura (somente leitura) da lista de clientes do Emissor NFS-e (app PHP
/// separado), para o painel SuperAdmin juntar com as lojas da Natural POR CNPJ.
/// Config: Nfse:BaseUrl (ex.: https://nfe.exemplo.com.br) e Nfse:Token (= o
/// API_CLIENTES_TOKEN do emissor). O token fica SÓ no servidor; o navegador
/// nunca o vê. Nunca lança: em falha, devolve Ok=false + lista vazia, para não
/// derrubar o painel caso o emissor esteja fora do ar.
/// </summary>
public class NfseClientesService(HttpClient http, IConfiguration config)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(config["Nfse:BaseUrl"]) && !string.IsNullOrWhiteSpace(config["Nfse:Token"]);

    public async Task<NfseClientesResultado> ListarAsync(CancellationToken ct = default)
    {
        if (!Configurado)
            return new NfseClientesResultado(false, "Integração NFS-e não configurada (Nfse:BaseUrl / Nfse:Token).", []);

        try
        {
            var baseUrl = config["Nfse:BaseUrl"]!.TrimEnd('/');
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/clientes.php");
            req.Headers.Add("X-Api-Token", config["Nfse:Token"]!);

            using var resp = await http.SendAsync(req, ct);
            var texto = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return new NfseClientesResultado(false, $"Emissor respondeu HTTP {(int)resp.StatusCode}.", []);

            var payload = JsonSerializer.Deserialize<NfsePayload>(texto, Json);
            return new NfseClientesResultado(true, null, payload?.Clientes ?? []);
        }
        catch (Exception ex)
        {
            return new NfseClientesResultado(false, $"Falha ao falar com o emissor: {ex.Message}", []);
        }
    }

    private sealed class NfsePayload
    {
        [JsonPropertyName("clientes")] public List<NfseClienteDto> Clientes { get; set; } = [];
    }
}

/// <summary>Cliente do emissor (contrato de /api/clientes.php). CNPJ e a chave de juncao.</summary>
public record NfseClienteDto(
    int Id,
    string Cnpj,
    [property: JsonPropertyName("razao_social")] string RazaoSocial,
    string Municipio,
    string Uf,
    string Ambiente,
    string Assinatura,
    [property: JsonPropertyName("trial_ate")] string? TrialAte,
    [property: JsonPropertyName("onboarding_completo")] bool OnboardingCompleto,
    [property: JsonPropertyName("certificado_validade")] string? CertificadoValidade,
    int Usuarios,
    [property: JsonPropertyName("notas_emitidas")] int NotasEmitidas,
    [property: JsonPropertyName("ultimo_acesso")] string? UltimoAcesso,
    [property: JsonPropertyName("criado_em")] string? CriadoEm
);

public record NfseClientesResultado(bool Ok, string? Erro, List<NfseClienteDto> Clientes);
