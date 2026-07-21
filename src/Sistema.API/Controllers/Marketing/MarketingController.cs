using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Marketing.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Marketing;

[ApiController]
[Route("api/marketing")]
[Authorize]
public class MarketingController(
    SistemaDbContext db, IUnitOfWork uow,
    Sistema.Infrastructure.Services.GeminiImageService gemini,
    Sistema.Infrastructure.Services.OpenAiImageService openaiImg,
    Sistema.Infrastructure.Services.ArteBrandingService branding) : ControllerBase
{
    // ─── Templates ────────────────────────────────────────────────────────────

    [HttpGet("templates")]
    public async Task<IActionResult> ListarTemplates([FromQuery] Guid empresaId,
        [FromQuery] string? tipo, CancellationToken ct)
    {
        var query = db.TemplatesMarketing.AsNoTracking()
            .Where(t => (t.EmpresaId == empresaId || t.EhTemplate) && t.Ativo);

        if (!string.IsNullOrEmpty(tipo) && Enum.TryParse<TipoArteMarketing>(tipo, out var tipoEnum))
            query = query.Where(t => t.Tipo == tipoEnum);

        return Ok(await query.OrderBy(t => t.Nome).ToListAsync(ct));
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CriarTemplate([FromBody] CriarTemplateRequest req, CancellationToken ct)
    {
        var tipo = Enum.Parse<TipoArteMarketing>(req.Tipo);
        var formato = Enum.Parse<FormatoArte>(req.Formato);
        var template = TemplateMarketing.Criar(req.EmpresaId, req.Nome, tipo, formato, req.LayoutJson);
        db.TemplatesMarketing.Add(template);
        await uow.SalvarAsync(ct);
        return Ok(new { template.Id });
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> AtualizarTemplate(Guid id, [FromBody] AtualizarTemplateRequest req, CancellationToken ct)
    {
        var template = await db.TemplatesMarketing.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Template não encontrado.");
        template.AtualizarLayout(req.LayoutJson, req.ThumbnailBase64);
        db.TemplatesMarketing.Update(template);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // ─── Artes ────────────────────────────────────────────────────────────────

    [HttpGet("artes")]
    public async Task<IActionResult> ListarArtes([FromQuery] Guid empresaId,
        [FromQuery] string? tipo, CancellationToken ct)
    {
        var query = db.ArtesMarketing.AsNoTracking().Where(a => a.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(tipo) && Enum.TryParse<TipoArteMarketing>(tipo, out var t))
            query = query.Where(a => a.Tipo == t);

        var lista = await query.OrderByDescending(a => a.CriadoEm).ToListAsync(ct);
        return Ok(lista.Select(a => new
        {
            a.Id,
            titulo = a.Nome,
            tipo = a.Tipo.ToString(),
            formato = a.Formato.ToString(),
            thumbnailUrl = a.UrlExportada,
            a.UrlExportada,
            a.LayoutJson,
            status = a.Status.ToString(),
            a.CriadoEm,
        }));
    }

    [HttpPost("artes")]
    public async Task<IActionResult> SalvarArte([FromBody] SalvarArteRequest req, CancellationToken ct)
    {
        // Herda tipo/formato do template (quando informado)
        var template = req.TemplateId.HasValue
            ? await db.TemplatesMarketing.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TemplateId, ct)
            : null;

        var tipo = template?.Tipo
            ?? (Enum.TryParse<TipoArteMarketing>(req.Tipo, out var tp) ? tp : TipoArteMarketing.Generico);
        var formato = template?.Formato
            ?? (Enum.TryParse<FormatoArte>(req.Formato, out var fm) ? fm : FormatoArte.FeedQuadrado);

        var arte = ArteMarketing.Criar(req.EmpresaId, req.Nome ?? "Arte sem título",
            tipo, formato, req.LayoutJson ?? "{}", req.TemplateId);
        db.ArtesMarketing.Add(arte);
        await uow.SalvarAsync(ct);
        return Ok(new { arte.Id });
    }

    [HttpPut("artes/{id:guid}")]
    public async Task<IActionResult> AtualizarArte(Guid id, [FromBody] SalvarArteRequest req, CancellationToken ct)
    {
        var arte = await db.ArtesMarketing.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Arte não encontrada.");
        arte.Renomear(req.Nome ?? arte.Nome);
        arte.AtualizarLayout(req.LayoutJson ?? "{}");
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("artes/{id:guid}")]
    public async Task<IActionResult> ExcluirArte(Guid id, CancellationToken ct)
    {
        var arte = await db.ArtesMarketing.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Arte não encontrada.");

        // Remove o arquivo de imagem gerado, se houver
        if (!string.IsNullOrEmpty(arte.UrlExportada))
        {
            var caminho = Path.Combine("wwwroot", arte.UrlExportada.TrimStart('/'));
            if (System.IO.File.Exists(caminho)) System.IO.File.Delete(caminho);
        }

        db.ArtesMarketing.Remove(arte);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Baixa o PNG gerado da arte.</summary>
    [HttpGet("artes/{id:guid}/exportar")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportarArte(Guid id, CancellationToken ct)
    {
        var arte = await db.ArtesMarketing.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (arte is null || string.IsNullOrEmpty(arte.UrlExportada))
            return NotFound(new { mensagem = "Imagem não disponível. Gere a arte com IA primeiro." });

        var caminho = Path.Combine("wwwroot", arte.UrlExportada.TrimStart('/'));
        if (!System.IO.File.Exists(caminho)) return NotFound(new { mensagem = "Arquivo não encontrado." });

        var bytes = await System.IO.File.ReadAllBytesAsync(caminho, ct);
        return File(bytes, "image/png", $"arte_{arte.Nome}.png");
    }

    // ─── Geração de imagem com IA (OpenAI gpt-image-1; Gemini como reserva) ────

    /// <summary>Gera a imagem da arte usando IA a partir de um prompt.
    /// Prefere a OpenAI (gpt-image-1); usa o Gemini como alternativa se a OpenAI não estiver configurada.</summary>
    [HttpPost("artes/gerar-ia")]
    public async Task<IActionResult> GerarArteIA([FromBody] GerarArteIaRequest req, CancellationToken ct)
    {
        if (!openaiImg.Configurado && !gemini.Configurado)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mensagem = "IA de imagem não configurada no servidor. Defina OpenAI:ApiKey (ou Gemini:ApiKey)."
            });

        var formato = Enum.TryParse<FormatoArte>(req.Formato, out var fm) ? fm : FormatoArte.FeedQuadrado;
        var tipo = Enum.TryParse<TipoArteMarketing>(req.Tipo, out var tp) ? tp : TipoArteMarketing.Generico;

        // Proporção (texto do prompt) e tamanho aceito pelo gpt-image-1
        var (proporcao, size) = formato switch
        {
            FormatoArte.StoryVertical    => ("proporção vertical 9:16 (Story/Reels)", "1024x1536"),
            FormatoArte.BannerHorizontal => ("proporção horizontal (banner)", "1536x1024"),
            _                            => ("proporção quadrada 1:1 (feed)", "1024x1024")
        };
        // Identidade da marca EcoGranel: paleta de cores injetada no prompt.
        // A logo oficial é sobreposta depois (a IA não reproduz o arquivo real).
        const string marca =
            "Siga a identidade visual da marca EcoGranel (produtos naturais): paleta em tons de marrom " +
            "(#5C2D0C e #8B4513), verde-folha (#6AAF2E) e fundo bege claro (#FAF7F4). Estilo natural, " +
            "orgânico, saudável e acolhedor. Deixe o canto superior esquerdo mais limpo e sem texto, com " +
            "espaço livre para a logomarca. Não escreva nenhum logotipo, marca d'água nem nome de marca na imagem.";
        var promptFinal = $"{req.Prompt}. Formato: {proporcao}. Arte publicitária profissional para redes sociais, " +
                          $"texto legível e bem posicionado, alta qualidade. {marca}";

        byte[] bytes;
        string gerador, modelo;
        try
        {
            if (openaiImg.Configurado)
            {
                bytes = await openaiImg.GerarImagemAsync(promptFinal, size, ct);
                gerador = "OpenAI"; modelo = openaiImg.ModeloAtual;
            }
            else
            {
                (bytes, _) = await gemini.GerarImagemAsync(promptFinal, ct);
                gerador = "GeminiNanoBanana2"; modelo = gemini.ModeloAtual;
            }
            // Sobrepõe a logo oficial da loja (canto inferior direito).
            bytes = await branding.AplicarLogoAsync(bytes, ct);
        }
        catch (Exception ex) { return BadRequest(new { mensagem = ex.Message }); }

        // Salva o PNG em wwwroot/uploads/artes
        var dir = Path.Combine("wwwroot", "uploads", "artes");
        Directory.CreateDirectory(dir);

        var arte = ArteMarketing.Criar(req.EmpresaId,
            string.IsNullOrWhiteSpace(req.Titulo) ? "Arte IA" : req.Titulo,
            tipo, formato,
            System.Text.Json.JsonSerializer.Serialize(new { gerador, prompt = req.Prompt }));

        var nomeArquivo = $"{arte.Id}.png";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, nomeArquivo), bytes, ct);
        arte.Finalizar($"/uploads/artes/{nomeArquivo}");

        db.ArtesMarketing.Add(arte);
        await uow.SalvarAsync(ct);

        return Ok(new { id = arte.Id, url = arte.UrlExportada, modelo });
    }

    // ─── Agendamentos ─────────────────────────────────────────────────────────

    [HttpGet("agendamentos")]
    public async Task<IActionResult> ListarAgendamentos([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lista = await (
            from a in db.AgendamentosPublicacao.AsNoTracking()
            join art in db.ArtesMarketing.AsNoTracking() on a.ArteMarketingId equals art.Id into arts
            from art in arts.DefaultIfEmpty()
            where a.EmpresaId == empresaId
            orderby a.DataHoraAgendada
            select new
            {
                a.Id, a.ArteMarketingId,
                arteTitulo = art != null ? art.Nome : "—",
                plataforma = a.Rede.ToString(),
                dataAgendamento = a.DataHoraAgendada,
                status = a.Status.ToString(),
                a.Legenda,
            }).ToListAsync(ct);
        return Ok(lista);
    }

    [HttpPost("agendamentos")]
    public async Task<IActionResult> Agendar([FromBody] AgendarPublicacaoRequest req, CancellationToken ct)
    {
        // Aceita "WhatsApp Status" / "WhatsAppStatus" e nomes com espaços
        var redeStr = (req.RedeSocial ?? req.Plataforma ?? "Instagram").Replace(" ", "");
        if (!Enum.TryParse<RedesSociais>(redeStr, ignoreCase: true, out var rede))
            rede = RedesSociais.Instagram;

        var dataHora = req.DataHoraAgendada ?? req.DataAgendamento ?? DateTime.UtcNow;
        if (req.ArteId == Guid.Empty)
            return BadRequest(new { mensagem = "Selecione uma arte para agendar." });

        var agendamento = AgendamentoPublicacao.Criar(req.EmpresaId, req.ArteId, rede, dataHora, req.Legenda);
        db.AgendamentosPublicacao.Add(agendamento);
        await uow.SalvarAsync(ct);
        return Ok(new { agendamento.Id });
    }

    [HttpDelete("agendamentos/{id:guid}")]
    public async Task<IActionResult> CancelarAgendamento(Guid id, CancellationToken ct)
    {
        var agendamento = await db.AgendamentosPublicacao.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Agendamento não encontrado.");
        agendamento.Cancelar();
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // ─── Geração automática por promoção ─────────────────────────────────────

    [HttpPost("artes/auto-promocao")]
    public async Task<IActionResult> GerarArtesPromocao(
        [FromBody] GerarArtePromocaoRequest req, CancellationToken ct)
    {
        var formatos = new[]
        {
            (FormatoArte.FeedQuadrado,   "Feed"),
            (FormatoArte.StoryVertical,  "Story"),
            (FormatoArte.BannerHorizontal, "Banner"),
        };

        var ids = new List<Guid>();
        foreach (var (formato, label) in formatos)
        {
            var layoutJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                gerador      = "AutoPromocao",
                formato      = formato.ToString(),
                nomePromocao = req.NomePromocao,
                desconto     = req.Desconto,
                tipoDesconto = req.TipoDesconto,
                tipoPromocao = req.TipoPromocao,
                aplicaEm     = req.AplicaEm,
                dataInicio   = req.DataInicio,
                dataFim      = req.DataFim,
                apenasClube  = req.ApenasClube,
            });

            var arte = ArteMarketing.Criar(
                req.EmpresaId,
                $"{req.NomePromocao} — {label}",
                TipoArteMarketing.Promocao,
                formato,
                layoutJson,
                templateId: null);

            db.ArtesMarketing.Add(arte);
            ids.Add(arte.Id);
        }

        await uow.SalvarAsync(ct);
        return Ok(new { ids });
    }
}

public record CriarTemplateRequest(Guid EmpresaId, string Nome, string Tipo, string Formato, string LayoutJson);
public record AtualizarTemplateRequest(string LayoutJson, string? ThumbnailBase64 = null);
public record SalvarArteRequest(
    Guid EmpresaId, string? Nome, Guid? TemplateId = null,
    string? LayoutJson = null, string? Tipo = null, string? Formato = null);

public record GerarArteIaRequest(
    Guid EmpresaId, string Prompt, string? Titulo = null,
    string? Formato = null, string? Tipo = null);

public record AgendarPublicacaoRequest(
    Guid EmpresaId, Guid ArteId,
    string? RedeSocial = null, string? Plataforma = null,
    DateTime? DataHoraAgendada = null, DateTime? DataAgendamento = null,
    string? Legenda = null);
public record GerarArtePromocaoRequest(
    Guid EmpresaId, string NomePromocao, decimal Desconto, string TipoDesconto,
    string TipoPromocao, string AplicaEm, string DataInicio, string? DataFim, bool ApenasClube);
