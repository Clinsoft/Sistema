using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Integração com o gateway Asaas (cobrança recorrente: Pix/boleto/cartão). Cria cliente e
/// assinatura; o restante (confirmação de pagamento) chega por webhook. Config: Asaas:ApiKey,
/// Asaas:BaseUrl (padrão sandbox). A chave fica SÓ no servidor.
/// </summary>
public class AsaasService(HttpClient http, IConfiguration config)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool Configurado => !string.IsNullOrWhiteSpace(config["Asaas:ApiKey"]);

    private HttpClient Cliente()
    {
        var baseUrl = (config["Asaas:BaseUrl"] ?? "https://sandbox.asaas.com/api/v3").TrimEnd('/') + "/";
        if (http.BaseAddress is null) http.BaseAddress = new Uri(baseUrl);
        if (!http.DefaultRequestHeaders.Contains("access_token"))
            http.DefaultRequestHeaders.Add("access_token", config["Asaas:ApiKey"] ?? "");
        if (!http.DefaultRequestHeaders.UserAgent.Any())
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NaturalSistemas/1.0");   // Asaas exige User-Agent
        return http;
    }

    /// <summary>Cria (ou reaproveita) o cliente no Asaas. Retorna o id (cus_...).</summary>
    public async Task<string> CriarClienteAsync(string nome, string cpfCnpj, string? email, string? telefone, CancellationToken ct = default)
    {
        var body = new { name = nome, cpfCnpj, email, mobilePhone = SoDigitos(telefone) };
        using var resp = await Cliente().PostAsJsonAsync("customers", body, Json, ct);
        var doc = await LerAsync(resp, ct);
        return doc.GetProperty("id").GetString()!;
    }

    /// <summary>Cria a assinatura recorrente. billingType UNDEFINED deixa o cliente escolher
    /// Pix/boleto/cartão na fatura. Retorna o id (sub_...).</summary>
    public async Task<string> CriarAssinaturaAsync(string customerId, decimal valor, DateOnly proximoVencimento,
        string ciclo, string descricao, CancellationToken ct = default)
    {
        var body = new
        {
            customer = customerId,
            billingType = "UNDEFINED",
            value = valor,
            nextDueDate = proximoVencimento.ToString("yyyy-MM-dd"),
            cycle = ciclo,   // MONTHLY | YEARLY
            description = descricao,
        };
        using var resp = await Cliente().PostAsJsonAsync("subscriptions", body, Json, ct);
        var doc = await LerAsync(resp, ct);
        return doc.GetProperty("id").GetString()!;
    }

    /// <summary>Link da primeira fatura da assinatura (página de pagamento Pix/boleto/cartão).</summary>
    public async Task<string?> ObterLinkFaturaAsync(string subscriptionId, CancellationToken ct = default)
    {
        using var resp = await Cliente().GetAsync($"subscriptions/{subscriptionId}/payments", ct);
        var doc = await LerAsync(resp, ct);
        if (doc.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            return data[0].TryGetProperty("invoiceUrl", out var url) ? url.GetString() : null;
        return null;
    }

    public async Task CancelarAssinaturaAsync(string subscriptionId, CancellationToken ct = default)
    {
        using var resp = await Cliente().DeleteAsync($"subscriptions/{subscriptionId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var texto = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Asaas {(int)resp.StatusCode}: {(texto.Length > 300 ? texto[..300] : texto)}");
        return JsonSerializer.Deserialize<JsonElement>(texto);
    }

    private static string? SoDigitos(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : new string(s.Where(char.IsDigit).ToArray());
}
