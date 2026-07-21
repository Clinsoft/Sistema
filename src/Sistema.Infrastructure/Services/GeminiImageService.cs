using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Geração de imagens via Google Gemini (Nano Banana 2 — Gemini 3.x Flash Image).
/// Usa a API Generative Language (generateContent) com resposta de modalidade IMAGE.
/// Chave e modelo são lidos da configuração: Gemini:ApiKey e Gemini:ImageModel.
/// </summary>
public class GeminiImageService(HttpClient http, IConfiguration config)
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public bool Configurado => !string.IsNullOrWhiteSpace(config["Gemini:ApiKey"]);

    public string ModeloAtual => config["Gemini:ImageModel"] ?? "gemini-3.1-flash-image-preview";

    public string ModeloTexto => config["Gemini:TextModel"] ?? "gemini-2.5-flash";

    /// <summary>
    /// Gera texto a partir de um prompt (usado, por ex., para sugerir a descrição
    /// complementar de um produto com seus benefícios). Retorna o texto puro.
    /// </summary>
    public async Task<string> GerarTextoAsync(string prompt, CancellationToken ct = default)
    {
        var apiKey = config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Chave da API Gemini não configurada. Defina 'Gemini:ApiKey' nas configurações do servidor.");
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt vazio.", nameof(prompt));

        var url = $"{BaseUrl}/{ModeloTexto}:generateContent?key={apiKey}";
        var payload = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = prompt } } }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erro do Gemini ({(int)resp.StatusCode}): {ExtrairErro(body)}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new InvalidOperationException($"Gemini não retornou texto. Resposta: {ExtrairErro(body)}");

        var sb = new StringBuilder();
        foreach (var part in candidates[0].GetProperty("content").GetProperty("parts").EnumerateArray())
            if (part.TryGetProperty("text", out var t))
                sb.Append(t.GetString());

        return sb.ToString().Trim();
    }

    /// <summary>Gera uma imagem a partir de um prompt textual. Retorna os bytes e o mime-type.</summary>
    public async Task<(byte[] Bytes, string Mime)> GerarImagemAsync(string prompt, CancellationToken ct = default)
    {
        var apiKey = config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Chave da API Gemini não configurada. Defina 'Gemini:ApiKey' (Nano Banana 2) nas configurações do servidor.");

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Descreva a arte a ser gerada (prompt).", nameof(prompt));

        var modelo = ModeloAtual;
        var url = $"{BaseUrl}/{modelo}:generateContent?key={apiKey}";

        var payload = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = prompt } } }
            },
            generationConfig = new { responseModalities = new[] { "IMAGE" } }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Erro do Gemini ({(int)resp.StatusCode}) ao gerar imagem: {ExtrairErro(body)}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new InvalidOperationException($"Gemini não retornou nenhuma imagem. Resposta: {ExtrairErro(body)}");

        foreach (var part in candidates[0].GetProperty("content").GetProperty("parts").EnumerateArray())
        {
            if (part.TryGetProperty("inlineData", out var inline) &&
                inline.TryGetProperty("data", out var data))
            {
                var mime = inline.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "image/png" : "image/png";
                return (Convert.FromBase64String(data.GetString()!), mime);
            }
        }

        throw new InvalidOperationException(
            $"O modelo respondeu, mas sem imagem. Verifique se '{modelo}' suporta geração de imagem. Resposta: {ExtrairErro(body)}");
    }

    private static string ExtrairErro(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? body;
            // Pode ter vindo texto em vez de imagem
            if (doc.RootElement.TryGetProperty("candidates", out var c) && c.GetArrayLength() > 0 &&
                c[0].TryGetProperty("content", out var ct) &&
                ct.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var t))
                return t.GetString() ?? body;
        }
        catch { /* ignora */ }
        return body.Length > 400 ? body[..400] : body;
    }
}
