using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Fiscal.Commands;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/nfes-recebidas")]
[Authorize]
public class NFesRecebidasController(SistemaDbContext db, IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] string? emitente,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] ManifestacaoTipo? manifestacao,
        CancellationToken ct)
    {
        var query = db.NotasFiscaisRecebidas.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(emitente))
            query = query.Where(n =>
                n.EmitenteNome.Contains(emitente) || n.EmitenteCnpj.Contains(emitente));

        if (dataInicio.HasValue)
            query = query.Where(n => n.DataEmissao >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(n => n.DataEmissao <= dataFim.Value.AddDays(1));

        if (manifestacao.HasValue)
            query = query.Where(n => n.Manifestacao == manifestacao.Value);

        var notas = await query
            .OrderByDescending(n => n.DataEmissao)
            .Select(n => new
            {
                n.Id,
                n.ChaveAcesso,
                n.NSU,
                n.Modelo,
                n.Serie,
                n.Numero,
                n.DataEmissao,
                n.EmitenteCnpj,
                n.EmitenteNome,
                n.EmitenteUF,
                n.ValorTotal,
                Situacao = n.Situacao.ToString(),
                Manifestacao = n.Manifestacao.HasValue ? n.Manifestacao.ToString() : null,
                n.DataManifestacao,
                n.JustificativaManifestacao,
                TemXml = n.XmlNota != null,
                n.DataConsulta,
                // Escrituração desta nota: permite ver na lista o que ficou pela metade
                EntradaId = db.EntradasNFe
                    .Where(e => e.NotaFiscalRecebidaId == n.Id)
                    .Select(e => (Guid?)e.Id).FirstOrDefault(),
                EntradaStatus = db.EntradasNFe
                    .Where(e => e.NotaFiscalRecebidaId == n.Id)
                    .Select(e => e.Status.ToString()).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return Ok(notas);
    }

    [HttpGet("{id:guid}/xml")]
    public async Task<IActionResult> BaixarXml(Guid id, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var nota = await db.NotasFiscaisRecebidas.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.EmpresaId == empresaId, ct);

        if (nota is null) return NotFound();
        if (nota.XmlNota is null)
            return BadRequest(new { mensagem = "XML não disponível. Manifeste a nota com Confirmação ou Ciência primeiro." });

        return File(
            System.Text.Encoding.UTF8.GetBytes(nota.XmlNota),
            "application/xml",
            $"NFe_{nota.ChaveAcesso}.xml");
    }

    [HttpPost("habilitar")]
    [Authorize(Roles = "Administrador,Financeiro,Contador")]
    public async Task<IActionResult> Habilitar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        // Verifica se o certificado está instalado
        var config = await db.ConfiguracoesFiscais.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        if (config is null)
            return BadRequest(new { mensagem = "Configure os dados fiscais antes de habilitar." });
        if (config.CertificadoPfxBase64 is null)
            return BadRequest(new { mensagem = "Instale o certificado digital A1 antes de habilitar." });

        // Tenta a primeira consulta ao SEFAZ
        var resultado = await mediator.Send(new ConsultarNFesRecebidasCommand(empresaId), ct);

        return Ok(new
        {
            habilitado = true,
            mensagem = resultado.Sucesso && resultado.NovasNotas > 0
                ? $"Monitoramento ativo! {resultado.NovasNotas} NF-e(s) importada(s)."
                : "Monitoramento ativo. As NF-e emitidas pelos seus fornecedores aparecerão aqui automaticamente quando chegarem na SEFAZ.",
            novasNFes = resultado.Sucesso ? resultado.NovasNotas : 0,
        });
    }

    [HttpPost("consultar")]
    [Authorize(Roles = "Administrador,Financeiro,Contador")]
    public async Task<IActionResult> Consultar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var resultado = await mediator.Send(new ConsultarNFesRecebidasCommand(empresaId), ct);
        return resultado.Sucesso
            ? Ok(resultado)
            : BadRequest(new { mensagem = resultado.Erro });
    }

    [HttpPost("{id:guid}/manifestar")]
    [Authorize(Roles = "Administrador,Financeiro,Contador")]
    public async Task<IActionResult> Manifestar(
        Guid id,
        [FromQuery] Guid empresaId,
        [FromBody] ManifestarRequest req,
        CancellationToken ct)
    {
        // O frontend envia o tipo como string (ex.: "CienciaOperacao"). A API não tem
        // conversor global de enum, então convertemos manualmente aqui.
        if (!Enum.TryParse<ManifestacaoTipo>(req.Tipo, ignoreCase: true, out var tipo))
            return BadRequest(new { mensagem = $"Tipo de manifestação inválido: '{req.Tipo}'." });

        var sucesso = await mediator.Send(new ManifestarNFeCommand(empresaId, id, tipo, req.Justificativa), ct);
        return Ok(new
        {
            sucesso,
            mensagem = sucesso
                ? "Manifestação registrada e aceita pela SEFAZ."
                : "Manifestação registrada localmente, mas a SEFAZ não confirmou o evento. Verifique o certificado e tente novamente."
        });
    }

    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var notas = await db.NotasFiscaisRecebidas.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId)
            .ToListAsync(ct);

        // Escriturações iniciadas e não finalizadas (ficam pela metade)
        var escrituracaoEmAberto = await db.EntradasNFe.AsNoTracking()
            .CountAsync(e => e.EmpresaId == empresaId && e.Status == StatusEntradaNFe.EmEdicao, ct);

        return Ok(new
        {
            Total = notas.Count,
            SemManifestacao = notas.Count(n => n.Manifestacao == null),
            Confirmadas = notas.Count(n => n.Manifestacao == ManifestacaoTipo.ConfirmacaoOperacao),
            Ciencia = notas.Count(n => n.Manifestacao == ManifestacaoTipo.CienciaOperacao),
            Desconhecidas = notas.Count(n => n.Manifestacao == ManifestacaoTipo.DesconhecimentoOperacao),
            NaoRealizadas = notas.Count(n => n.Manifestacao == ManifestacaoTipo.OperacaoNaoRealizada),
            ValorTotal = notas.Sum(n => n.ValorTotal),
            EscrituracaoEmAberto = escrituracaoEmAberto,
        });
    }
}

public record ManifestarRequest(string Tipo, string? Justificativa);
