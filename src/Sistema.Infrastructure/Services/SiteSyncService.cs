using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Envia os produtos para o site público (ecogranel.com.br) via HTTP POST em JSON,
/// autenticado por um token compartilhado. O site recebe num endpoint (sync.php).
/// Config: Site:SyncUrl e Site:SyncToken.
/// </summary>
public class SiteSyncService(HttpClient http, IConfiguration config)
{
    public bool Configurado => !string.IsNullOrWhiteSpace(config["Site:SyncUrl"]);

    /// <summary>URL pública base para montar links de imagem (ex.: a API que serve /uploads).</summary>
    public string BaseImagens => config["Site:PublicBaseUrl"] ?? "https://sistema.ecogranel.com.br";

    /// <summary>
    /// Envia a lista de produtos ao site. Retorna (sucesso, quantidade, mensagem).
    /// </summary>
    public async Task<(bool Ok, int Qtd, string Mensagem)> EnviarAsync(
        IReadOnlyList<object> produtos, CancellationToken ct = default)
    {
        var url = config["Site:SyncUrl"];
        if (string.IsNullOrWhiteSpace(url))
            return (false, 0, "Sincronização com o site não configurada (Site:SyncUrl).");

        var payload = new
        {
            token = config["Site:SyncToken"] ?? "",
            produtos
        };
        var json = JsonSerializer.Serialize(payload);

        try
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await http.PostAsync(url, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return (false, 0, $"Site respondeu {(int)resp.StatusCode}: {Trunc(body)}");
            return (true, produtos.Count, "Sincronizado com o site.");
        }
        catch (Exception ex)
        {
            return (false, 0, $"Falha ao contatar o site: {ex.Message}");
        }
    }

    /// <summary>slugify igual ao usado no QR Code da etiqueta (para o ?p= bater).</summary>
    public static string Slugify(string nome)
    {
        var s = (nome ?? "").ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '-') sb.Append(' ');
        }
        var limpo = System.Text.RegularExpressions.Regex.Replace(sb.ToString().Trim(), @"\s+", "-");
        return limpo;
    }

    private static string Trunc(string s) => s.Length > 300 ? s[..300] : s;
}
