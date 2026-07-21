using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Geração de texto via OpenAI (Chat Completions). Usado, por ex., para sugerir a
/// descrição complementar de um produto com seus benefícios.
/// Chave e modelo lidos da configuração: OpenAI:ApiKey e OpenAI:Model.
/// </summary>
public class OpenAiTextService(HttpClient http, IConfiguration config)
{
    private const string Url = "https://api.openai.com/v1/chat/completions";

    public bool Configurado => !string.IsNullOrWhiteSpace(config["OpenAI:ApiKey"]);

    public string ModeloAtual => config["OpenAI:Model"] ?? "gpt-4o-mini";

    /// <summary>Gera texto a partir de um prompt do usuário. Retorna o texto puro.</summary>
    public async Task<string> GerarTextoAsync(string prompt, CancellationToken ct = default)
    {
        var apiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Chave da API OpenAI não configurada. Defina 'OpenAI:ApiKey' nas configurações do servidor.");
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt vazio.", nameof(prompt));

        var payload = new
        {
            model = ModeloAtual,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 400
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Bearer {apiKey}");

        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erro da OpenAI ({(int)resp.StatusCode}): {ExtrairErro(body)}");

        using var doc = JsonDocument.Parse(body);
        var texto = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return (texto ?? string.Empty).Trim();
    }

    private static string ExtrairErro(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? body;
        }
        catch { /* ignora */ }
        return body.Length > 400 ? body[..400] : body;
    }
}
