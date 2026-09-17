using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sistema.API.Controllers.Relatorios;

/// <summary>
/// Análise por IA de qualquer painel do Dashboard. O frontend envia o título do
/// card e um resumo textual dos dados que já estão na tela; a IA responde com
/// leitura analítica + sugestão de condução, no papel de um administrador sênior.
/// </summary>
[ApiController]
[Route("api/relatorios")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
public class AnaliseIaController(Sistema.Infrastructure.Services.OpenAiTextService ia) : ControllerBase
{
    /// <summary>Persona reaproveitada por todas as análises do Dashboard.</summary>
    public const string PersonaAdmin =
        "Você é um ADMINISTRADOR SÊNIOR de uma rede de lojas de produtos naturais a granel, " +
        "com competências plenas e sêniores em gestão financeira, comercial e operacional de varejo. " +
        "Interprete os dados com visão de dono: identifique causas, riscos e oportunidades e traduza " +
        "tudo em decisões práticas e priorizadas. Seja objetivo e direto, use apenas os números " +
        "fornecidos (não invente valores) e escreva em português do Brasil, em Markdown.";

    public record AnaliseCardRequest(string Titulo, string Dados);

    [HttpPost("analise-card")]
    public async Task<IActionResult> AnaliseCard([FromBody] AnaliseCardRequest req, CancellationToken ct)
    {
        if (!ia.Configurado)
            return BadRequest(new { mensagem = "IA não configurada (OpenAI:ApiKey) no servidor." });
        if (req is null || string.IsNullOrWhiteSpace(req.Dados))
            return Ok(new { analise = "Sem dados suficientes para analisar este painel." });

        var dados = req.Dados.Length > 6000 ? req.Dados[..6000] : req.Dados;
        var titulo = string.IsNullOrWhiteSpace(req.Titulo) ? "painel do Dashboard" : req.Titulo.Trim();

        var prompt =
            PersonaAdmin + "\n\n" +
            $"Analise o painel \"{titulo}\" do Dashboard de gestão. Dados apresentados na tela:\n" +
            dados + "\n\n" +
            "Responda em Markdown, objetivo, com estas seções:\n" +
            "**Leitura dos números** (2 a 4 frases sobre o que os dados dizem e o que chama atenção).\n" +
            "**Pontos de atenção** (bullets com riscos/oportunidades concretos).\n" +
            "**Como conduzir** (3 a 5 ações práticas e priorizadas). Não invente números além dos fornecidos.";

        try
        {
            var analise = await ia.GerarTextoAsync(prompt, ct, maxTokens: 700);
            return Ok(new { analise, modelo = ia.ModeloAtual });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { mensagem = "Falha ao gerar a análise: " + ex.Message });
        }
    }
}
