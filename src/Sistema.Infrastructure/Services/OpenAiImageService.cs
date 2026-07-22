using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Geração de imagens via OpenAI (Images API — gpt-image-1). Usado para criar as
/// artes de marketing para redes sociais.
/// Chave e modelo lidos da configuração: OpenAI:ApiKey e OpenAI:ImageModel.
/// </summary>
public class OpenAiImageService(HttpClient http, IConfiguration config)
{
    private const string Url = "https://api.openai.com/v1/images/generations";
    private const string EditUrl = "https://api.openai.com/v1/images/edits";

    public bool Configurado => !string.IsNullOrWhiteSpace(config["OpenAI:ApiKey"]);

    public string ModeloAtual => config["OpenAI:ImageModel"] ?? "gpt-image-1";

    /// <summary>
    /// Gera uma imagem PNG a partir de um prompt. `size` deve ser um tamanho aceito
    /// pelo modelo (ex.: "1024x1024", "1024x1536", "1536x1024"). Retorna os bytes PNG.
    /// </summary>
    public async Task<byte[]> GerarImagemAsync(string prompt, string size = "1024x1024", CancellationToken ct = default)
    {
        var apiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Chave da API OpenAI não configurada. Defina 'OpenAI:ApiKey' nas configurações do servidor.");
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Descreva a arte a ser gerada (prompt).", nameof(prompt));

        var payload = new
        {
            model = ModeloAtual,
            prompt,
            size,
            n = 1
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Authorization", $"Bearer {apiKey}");

        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erro da OpenAI ({(int)resp.StatusCode}) ao gerar imagem: {ExtrairErro(body)}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            throw new InvalidOperationException($"OpenAI não retornou imagem. Resposta: {ExtrairErro(body)}");

        var item = data[0];
        // gpt-image-1 retorna sempre b64_json; dall-e pode retornar url.
        if (item.TryGetProperty("b64_json", out var b64) && b64.GetString() is { Length: > 0 } s)
            return Convert.FromBase64String(s);

        throw new InvalidOperationException(
            $"O modelo respondeu, mas sem imagem em base64. Resposta: {ExtrairErro(body)}");
    }

    /// <summary>
    /// Cria uma arte A PARTIR de uma imagem base (image-to-image) via /v1/images/edits.
    /// Envia a foto real do produto como referência e o prompt com as condições; o
    /// modelo integra o produto na arte de forma natural. `imagemPng` deve ser PNG.
    /// </summary>
    public async Task<byte[]> EditarImagemAsync(byte[] imagemPng, string prompt,
        string size = "1024x1024", CancellationToken ct = default)
    {
        var apiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Chave da API OpenAI não configurada. Defina 'OpenAI:ApiKey' nas configurações do servidor.");
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Descreva a arte a ser gerada (prompt).", nameof(prompt));

        using var form = new MultipartFormDataContent
        {
            { new StringContent(ModeloAtual), "model" },
            { new StringContent(prompt),      "prompt" },
            { new StringContent(size),        "size" },
            { new StringContent("1"),         "n" },
        };
        var imgContent = new ByteArrayContent(imagemPng);
        imgContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(imgContent, "image", "produto.png");

        using var req = new HttpRequestMessage(HttpMethod.Post, EditUrl) { Content = form };
        req.Headers.Add("Authorization", $"Bearer {apiKey}");

        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Erro da OpenAI ({(int)resp.StatusCode}) ao editar imagem: {ExtrairErro(body)}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            throw new InvalidOperationException($"OpenAI não retornou imagem. Resposta: {ExtrairErro(body)}");

        var item = data[0];
        if (item.TryGetProperty("b64_json", out var b64) && b64.GetString() is { Length: > 0 } s)
            return Convert.FromBase64String(s);

        throw new InvalidOperationException(
            $"O modelo respondeu, mas sem imagem em base64. Resposta: {ExtrairErro(body)}");
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
