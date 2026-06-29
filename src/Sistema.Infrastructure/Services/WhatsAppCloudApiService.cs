using Microsoft.Extensions.Logging;
using Sistema.Domain.WhatsApp.Entities;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Envia mensagens via Meta WhatsApp Cloud API (Graph API v19).
/// Documentação: https://developers.facebook.com/docs/whatsapp/cloud-api/messages
/// </summary>
public class WhatsAppCloudApiService(HttpClient http, ILogger<WhatsAppCloudApiService> logger)
{
    private const string BaseUrl = "https://graph.facebook.com/v19.0";

    /// <summary>
    /// Envia uma mensagem de template para um número de telefone.
    /// O template deve estar aprovado na Meta Business Manager.
    /// </summary>
    public async Task<(bool sucesso, string? wamId, string? erro)> EnviarTemplate(
        string phoneNumberId,
        string accessToken,
        string telefone,
        string templateName,
        string idioma,
        IEnumerable<string> variaveis)
    {
        // Normaliza o telefone: apenas dígitos, com código do país
        var tel = NormalizarTelefone(telefone);

        var payload = new
        {
            messaging_product = "whatsapp",
            to = tel,
            type = "template",
            template = new
            {
                name     = templateName,
                language = new { code = idioma },
                components = variaveis.Any() ? new object[]
                {
                    new
                    {
                        type       = "body",
                        parameters = variaveis.Select(v => new { type = "text", text = v }).ToArray()
                    }
                } : Array.Empty<object>()
            }
        };

        try
        {
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await http.PostAsJsonAsync($"{BaseUrl}/{phoneNumberId}/messages", payload);
            var body     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("[WhatsApp] Falha ao enviar para {Tel}: {Status} — {Body}",
                    tel, response.StatusCode, body);
                return (false, null, body);
            }

            // Extrai o wam_id da resposta
            using var doc = JsonDocument.Parse(body);
            var wamId = doc.RootElement
                .GetProperty("messages")[0]
                .GetProperty("id")
                .GetString();

            logger.LogInformation("[WhatsApp] ✅ Enviado para {Tel} | wam_id={WamId}", tel, wamId);
            return (true, wamId, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WhatsApp] Exceção ao enviar para {Tel}", tel);
            return (false, null, ex.Message);
        }
    }

    /// <summary>Lista os templates aprovados na conta WABA via Graph API.</summary>
    public async Task<List<MetaTemplateDto>> ListarTemplatesAprovados(
        string businessAccountId, string accessToken)
    {
        try
        {
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"{BaseUrl}/{businessAccountId}/message_templates?fields=name,status,category,language,components&limit=100";
            var resp = await http.GetFromJsonAsync<MetaTemplatesResponse>(url);
            return resp?.Data?.Where(t => t.Status == "APPROVED").ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WhatsApp] Erro ao listar templates");
            return [];
        }
    }

    private static string NormalizarTelefone(string tel)
    {
        tel = new string(tel.Where(char.IsDigit).ToArray());
        // Se não começar com 55 (Brasil), adiciona
        if (!tel.StartsWith("55") && tel.Length <= 11)
            tel = "55" + tel;
        return tel;
    }
}

public class MetaTemplatesResponse
{
    [JsonPropertyName("data")]
    public List<MetaTemplateDto>? Data { get; set; }
}

public class MetaTemplateDto
{
    [JsonPropertyName("name")]     public string Name     { get; set; } = "";
    [JsonPropertyName("status")]   public string Status   { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("language")] public string Language { get; set; } = "";
}
