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

    /// <summary>Produto no formato do catálogo comercial da Meta.</summary>
    public record CatalogoProdutoMeta(string RetailerId, string Name, string? Description,
        int PriceCents, string? ImageUrl, string? Url, bool Disponivel);

    /// <summary>
    /// Envia (upsert) uma lista de produtos ao catálogo comercial da Meta via /{catalog_id}/batch.
    /// A Meta exige imagem por produto — os sem imagem devem ser filtrados antes.
    /// </summary>
    public async Task<(bool Ok, int Enviados, string Mensagem)> SincronizarCatalogoAsync(
        string catalogId, string accessToken, IReadOnlyList<CatalogoProdutoMeta> produtos,
        CancellationToken ct = default)
    {
        if (produtos.Count == 0) return (true, 0, "Nenhum produto (com foto) para enviar.");

        var requests = produtos.Select(p => new
        {
            method = "UPDATE", // upsert pelo retailer_id
            retailer_id = p.RetailerId,
            data = new
            {
                name = p.Name,
                description = string.IsNullOrWhiteSpace(p.Description) ? p.Name : p.Description,
                url = p.Url ?? "",
                image_url = p.ImageUrl,
                price = p.PriceCents,
                currency = "BRL",
                availability = p.Disponivel ? "in stock" : "out of stock",
                condition = "new",
            }
        }).ToArray();

        try
        {
            // A Meta espera `requests` como campo de formulário contendo o JSON em string
            // (não um corpo JSON com propriedade "requests").
            var requestsJson = JsonSerializer.Serialize(requests);
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["requests"]     = requestsJson,
                ["access_token"] = accessToken,
            });
            var resp = await http.PostAsync($"{BaseUrl}/{catalogId}/batch", form, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Falha ao sincronizar catálogo Meta: {Body}", body);
                var msg = body.Length > 300 ? body[..300] : body;
                return (false, 0, $"Meta {(int)resp.StatusCode}: {msg}");
            }
            return (true, produtos.Count, "Catálogo sincronizado com a Meta.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao sincronizar catálogo Meta");
            return (false, 0, ex.Message);
        }
    }

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
        IEnumerable<string> variaveis,
        string? headerImageUrl = null)
    {
        // Normaliza o telefone: apenas dígitos, com código do país
        var tel = NormalizarTelefone(telefone);

        // Monta os componentes: cabeçalho de imagem (se o template exigir) + corpo.
        var componentes = new List<object>();
        if (!string.IsNullOrWhiteSpace(headerImageUrl))
        {
            componentes.Add(new
            {
                type       = "header",
                parameters = new object[] { new { type = "image", image = new { link = headerImageUrl } } }
            });
        }
        if (variaveis.Any())
        {
            componentes.Add(new
            {
                type       = "body",
                parameters = variaveis.Select(v => new { type = "text", text = v }).ToArray()
            });
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = tel,
            type = "template",
            template = new
            {
                name     = templateName,
                language = new { code = idioma },
                components = componentes.ToArray()
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

    /// <summary>Lista os templates aprovados na conta WABA via Graph API.
    /// Retorna também o erro cru da Meta quando a chamada falha, para diagnóstico de credenciais.</summary>
    public async Task<(List<MetaTemplateDto> Templates, string? Erro)> ListarTemplatesAprovados(
        string businessAccountId, string accessToken)
    {
        try
        {
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"{BaseUrl}/{businessAccountId}/message_templates?fields=name,status,category,language,components&limit=100";
            var resp = await http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("[WhatsApp] Meta recusou listagem de templates: {Status} — {Body}",
                    resp.StatusCode, body);
                // Extrai a mensagem de erro amigável da Meta, se houver.
                var erro = TentarExtrairErroMeta(body) ?? $"Meta {(int)resp.StatusCode}";
                return ([], erro);
            }

            var dados = JsonSerializer.Deserialize<MetaTemplatesResponse>(body);
            var aprovados = dados?.Data?.Where(t => t.Status == "APPROVED").ToList() ?? [];
            return (aprovados, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WhatsApp] Erro ao listar templates");
            return ([], ex.Message);
        }
    }

    /// <summary>Lê o campo error.message do corpo de erro padrão da Graph API.</summary>
    private static string? TentarExtrairErroMeta(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg  = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                var tipo = err.TryGetProperty("type", out var t) ? t.GetString() : null;
                return string.IsNullOrWhiteSpace(tipo) ? msg : $"{msg} ({tipo})";
            }
        }
        catch { /* corpo não-JSON */ }
        return null;
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
