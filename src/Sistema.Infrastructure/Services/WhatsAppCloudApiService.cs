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

    /// <summary>
    /// Cria um modelo (template) de mensagem na Meta e o envia para análise.
    /// Categoria MARKETING, corpo com variáveis {{1}}..{{N}} e, opcionalmente, cabeçalho
    /// de imagem — que exige subir a imagem de exemplo (resumable upload) e usar o handle.
    /// </summary>
    public async Task<(bool Ok, string? Status, string? Erro)> CriarTemplateAsync(
        string wabaId, string accessToken, string? appId,
        string nome, string corpo, IReadOnlyList<string> exemplos,
        byte[]? imagemExemploPng, CancellationToken ct = default)
    {
        try
        {
            var componentes = new List<object>();

            // Cabeçalho de imagem (opcional): precisa do handle da imagem de exemplo.
            if (imagemExemploPng is { Length: > 0 })
            {
                // O App ID informado costuma vir errado (às vezes é o Phone Number ID);
                // descobre o App ID real a partir do próprio token via debug_token.
                var appIdReal = await ObterAppIdDoTokenAsync(accessToken, ct) ?? appId;
                if (string.IsNullOrWhiteSpace(appIdReal))
                    return (false, null, "Não foi possível determinar o App ID a partir do token.");

                var handle = await SubirImagemExemploAsync(appIdReal, accessToken, imagemExemploPng, ct);
                if (handle is null)
                    return (false, null, "Não foi possível subir a imagem de exemplo do cabeçalho na Meta.");
                componentes.Add(new
                {
                    type = "HEADER",
                    format = "IMAGE",
                    example = new { header_handle = new[] { handle } }
                });
            }

            // Corpo com exemplos das variáveis (a Meta exige exemplos para aprovar).
            componentes.Add(exemplos.Count > 0
                ? new { type = "BODY", text = corpo, example = new { body_text = new[] { exemplos.ToArray() } } }
                : (object)new { type = "BODY", text = corpo });

            var payload = new
            {
                name = nome,
                language = "pt_BR",
                category = "MARKETING",
                components = componentes.ToArray()
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{wabaId}/message_templates")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("[WhatsApp] Falha ao criar template: {Body}", body);
                return (false, null, TentarExtrairErroMeta(body) ?? $"Meta {(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : "PENDING";
            return (true, status, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WhatsApp] Erro ao criar template");
            return (false, null, ex.Message);
        }
    }

    /// <summary>Descobre o App ID da Meta a partir do próprio token (debug_token).</summary>
    private async Task<string?> ObterAppIdDoTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}/debug_token?input_token={Uri.EscapeDataString(token)}" +
                      $"&access_token={Uri.EscapeDataString(token)}";
            using var resp = await http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) { logger.LogWarning("[WhatsApp] debug_token falhou: {B}", body); return null; }
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var d) &&
                d.TryGetProperty("app_id", out var a))
                return a.GetString();
        }
        catch (Exception ex) { logger.LogWarning(ex, "[WhatsApp] erro ao obter app_id do token"); }
        return null;
    }

    /// <summary>Sobe a imagem de exemplo via Resumable Upload e devolve o handle usado no template.</summary>
    private async Task<string?> SubirImagemExemploAsync(
        string appId, string accessToken, byte[] png, CancellationToken ct)
    {
        // 1) Abre a sessão de upload.
        var startUrl = $"{BaseUrl}/{appId}/uploads?file_name=header.png&file_length={png.Length}" +
                       $"&file_type=image/png&access_token={Uri.EscapeDataString(accessToken)}";
        using var startResp = await http.PostAsync(startUrl, null, ct);
        var startBody = await startResp.Content.ReadAsStringAsync(ct);
        if (!startResp.IsSuccessStatusCode) { logger.LogWarning("[WhatsApp] upload start falhou: {B}", startBody); return null; }
        using var startDoc = JsonDocument.Parse(startBody);
        var sessionId = startDoc.RootElement.TryGetProperty("id", out var sid) ? sid.GetString() : null;
        if (string.IsNullOrEmpty(sessionId)) return null;

        // 2) Envia os bytes; a resposta traz o handle em "h".
        using var upReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{sessionId}")
        {
            Content = new ByteArrayContent(png)
        };
        upReq.Headers.TryAddWithoutValidation("Authorization", $"OAuth {accessToken}");
        upReq.Headers.TryAddWithoutValidation("file_offset", "0");

        using var upResp = await http.SendAsync(upReq, ct);
        var upBody = await upResp.Content.ReadAsStringAsync(ct);
        if (!upResp.IsSuccessStatusCode) { logger.LogWarning("[WhatsApp] upload bytes falhou: {B}", upBody); return null; }
        using var upDoc = JsonDocument.Parse(upBody);
        return upDoc.RootElement.TryGetProperty("h", out var h) ? h.GetString() : null;
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
