using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Fiscal.Commands;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Financeiro.Entities;
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
        [FromQuery] string? modelo,
        CancellationToken ct)
    {
        var query = db.NotasFiscaisRecebidas.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId);

        // Modelo 57 = CT-e (frete). Quando não pedido, a listagem de NF-e exclui os CT-e
        // (que têm tela própria); quando modelo="57", traz apenas os CT-e.
        if (modelo == "57")
            query = query.Where(n => n.Modelo == "57");
        else if (!string.IsNullOrWhiteSpace(modelo))
            query = query.Where(n => n.Modelo == modelo);
        else
            query = query.Where(n => n.Modelo != "57");

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
                // CT-e: se o frete já foi lançado como conta a pagar.
                Lancado = db.LancamentosFinanceiros
                    .Any(l => l.EmpresaId == empresaId && l.DocumentoOrigem == "CT-e " + n.ChaveAcesso),
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

    /// <summary>Lança o frete de um CT-e como conta a pagar, vinculada à transportadora.</summary>
    [HttpPost("{id:guid}/lancar-financeiro")]
    [Authorize(Roles = "Administrador,Financeiro,Contador")]
    public async Task<IActionResult> LancarFinanceiro(
        Guid id, [FromBody] LancarFreteRequest req, CancellationToken ct)
    {
        var cte = await db.NotasFiscaisRecebidas.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.EmpresaId == req.EmpresaId, ct);
        if (cte is null) return NotFound();
        if (cte.Modelo != "57")
            return BadRequest(new { mensagem = "Este documento não é um CT-e." });
        if (cte.ValorTotal <= 0)
            return BadRequest(new { mensagem = "CT-e sem valor de frete para lançar." });

        var docOrigem = $"CT-e {cte.ChaveAcesso}";

        // Evita duplicar: já existe um lançamento para este CT-e?
        var jaLancado = await db.LancamentosFinanceiros
            .AnyAsync(l => l.EmpresaId == req.EmpresaId && l.DocumentoOrigem == docOrigem, ct);
        if (jaLancado)
            return Conflict(new { mensagem = "Este CT-e já foi lançado no financeiro." });

        // Casa a transportadora com um fornecedor cadastrado pelo CNPJ (se houver).
        var cnpjDigitos = new string((cte.EmitenteCnpj ?? "").Where(char.IsDigit).ToArray());
        Guid? fornecedorId = null;
        if (cnpjDigitos.Length > 0)
            fornecedorId = await db.Fornecedores
                .Where(f => f.EmpresaId == req.EmpresaId && f.Cnpj == cnpjDigitos)
                .Select(f => (Guid?)f.Id).FirstOrDefaultAsync(ct);

        var vencimento = req.DataVencimento ?? DateTime.Today.AddDays(req.DiasVencimento ?? 7);

        var lanc = LancamentoFinanceiro.Criar(
            req.EmpresaId, Sistema.Domain.Financeiro.Entities.TipoLancamento.ContaPagar,
            $"Frete CT-e {cte.Numero} — {cte.EmitenteNome}", cte.ValorTotal, vencimento,
            fornecedorId: fornecedorId, categoriaId: req.CategoriaId,
            documentoOrigem: docOrigem);
        lanc.DefinirClassificacao("Frete", cte.EmitenteNome, $"CT-e {cte.ChaveAcesso}");

        db.LancamentosFinanceiros.Add(lanc);
        await db.SaveChangesAsync(ct);

        return Ok(new { id = lanc.Id, fornecedorVinculado = fornecedorId != null });
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
            .Where(n => n.EmpresaId == empresaId && n.Modelo != "57")   // CT-e têm tela própria
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

public record LancarFreteRequest(
    Guid EmpresaId, Guid? CategoriaId = null, DateTime? DataVencimento = null, int? DiasVencimento = null);
