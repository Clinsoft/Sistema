using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/configuracao")]
[Authorize(Roles = "Administrador,Contador")]
public class ConfiguracaoFiscalController(
    IConfiguracaoFiscalRepository repo, IUnitOfWork uow, SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var config = await repo.ObterPorEmpresaAsync(empresaId, ct);
        return config is null ? NotFound("Configuração fiscal não encontrada.") : Ok(config);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarConfiguracaoRequest req, CancellationToken ct)
    {
        var existe = await repo.ObterPorEmpresaAsync(req.EmpresaId, ct);
        if (existe is not null)
            throw new InvalidOperationException("Configuração fiscal já existe para esta empresa.");

        var regime = Enum.Parse<RegimeTributario>(req.Regime);
        var config = ConfiguracaoFiscal.Criar(req.EmpresaId, regime);

        if (!string.IsNullOrEmpty(req.CscId) && !string.IsNullOrEmpty(req.CscToken))
            config.ConfigurarNFCe(req.CscId, req.CscToken);

        await repo.AdicionarAsync(config, ct);
        await uow.SalvarAsync(ct);
        return Ok(new { config.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id,
        [FromBody] System.Text.Json.JsonElement body, CancellationToken ct)
    {
        var config = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Configuração não encontrada.");

        // Regime
        if (body.TryGetProperty("regime", out var regimeProp))
        {
            var regimeStr = regimeProp.ValueKind == System.Text.Json.JsonValueKind.Number
                ? ((RegimeTributario)regimeProp.GetInt32()).ToString()
                : regimeProp.GetString() ?? "";
            if (Enum.TryParse<RegimeTributario>(regimeStr, out var regime))
                config.AtualizarRegime(regime);
        }

        // Ambiente
        if (body.TryGetProperty("ambiente", out var ambProp))
        {
            var ambVal = ambProp.ValueKind == System.Text.Json.JsonValueKind.Number
                ? ambProp.GetInt32() : -1;
            var ambStr = ambProp.ValueKind == System.Text.Json.JsonValueKind.String
                ? ambProp.GetString() ?? "" : "";

            if (ambVal == 1 || ambStr.ToLower() == "producao")
                config.IrParaProducao();
            else if (ambVal == 2 || ambStr.ToLower() == "homologacao")
                config.IrParaHomologacao();
        }

        // CSC NFC-e — id pode vir como number ou string do frontend
        if (body.TryGetProperty("cscIdNFCe", out var cscId) &&
            body.TryGetProperty("cscTokenNFCe", out var cscToken))
        {
            var id2 = cscId.ValueKind == System.Text.Json.JsonValueKind.Number
                ? cscId.GetInt32().ToString()
                : cscId.GetString() ?? "";
            var tok = cscToken.ValueKind == System.Text.Json.JsonValueKind.Number
                ? cscToken.GetInt32().ToString()
                : cscToken.GetString() ?? "";
            if (!string.IsNullOrEmpty(id2) && !string.IsNullOrEmpty(tok))
                config.ConfigurarNFCe(id2, tok);
        }

        repo.Atualizar(config);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/producao")]
    public async Task<IActionResult> IrParaProducao(Guid id, CancellationToken ct)
    {
        var config = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Configuração não encontrada.");
        config.IrParaProducao();
        repo.Atualizar(config);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/homologacao")]
    public async Task<IActionResult> IrParaHomologacao(Guid id, CancellationToken ct)
    {
        var config = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Configuração não encontrada.");
        config.IrParaHomologacao();
        repo.Atualizar(config);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Aplica tributação padrão (ICMS/PIS/COFINS) a todos os produtos da empresa
    /// conforme o regime tributário configurado. Não altera NCM/CEST.
    /// </summary>
    [HttpPost("{id:guid}/aplicar-tributacao-padrao")]
    public async Task<IActionResult> AplicarTributacaoPadrao(
        Guid id, [FromQuery] bool apenasSeVazio = false, CancellationToken ct = default)
    {
        var config = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Configuração não encontrada.");

        var regime = config.Regime.ToString();

        var produtos = await db.Produtos
            .Where(p => p.EmpresaId == config.EmpresaId)
            .ToListAsync(ct);

        int atualizados = 0;
        foreach (var produto in produtos)
        {
            // Se apenasSeVazio=true, só aplica em produtos sem tributação definida
            if (apenasSeVazio &&
                (produto.CstIcms != null || produto.CsosnIcms != null))
                continue;

            produto.AplicarTributacaoPadrao(regime);
            atualizados++;
        }

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            regime,
            totalProdutos = produtos.Count,
            atualizados,
            padrao = TributacaoPadrao(regime)
        });
    }

    /// <summary>Retorna os valores de tributação padrão para um regime, sem alterar dados.</summary>
    [HttpGet("{id:guid}/tributacao-padrao")]
    public async Task<IActionResult> ObterTributacaoPadrao(Guid id, CancellationToken ct)
    {
        var config = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Configuração não encontrada.");

        var regime = config.Regime.ToString();
        var totalProdutos = await db.Produtos
            .CountAsync(p => p.EmpresaId == config.EmpresaId, ct);
        var semTributacao = await db.Produtos
            .CountAsync(p => p.EmpresaId == config.EmpresaId &&
                             p.CstIcms == null && p.CsosnIcms == null, ct);

        return Ok(new
        {
            regime,
            totalProdutos,
            semTributacao,
            padrao = TributacaoPadrao(regime)
        });
    }

    private static object TributacaoPadrao(string regime) => regime switch
    {
        "SimplesNacional" => new
        {
            csosnIcms = "400", cstIcms = (string?)null, aliquotaIcms = 0m,
            cstPisCofins = "07", aliquotaPis = 0m, aliquotaCofins = 0m, cfop = "5102",
            descricao = "CSOSN 400 — Não tributado (SN). PIS/COFINS CST 07 — isento."
        },
        "LucroPresumido" => new
        {
            csosnIcms = (string?)null, cstIcms = "000", aliquotaIcms = 12m,
            cstPisCofins = "01", aliquotaPis = 0.65m, aliquotaCofins = 3m, cfop = "5102",
            descricao = "CST 000 — tributado integral. PIS 0,65% / COFINS 3% (regime cumulativo)."
        },
        "LucroReal" => new
        {
            csosnIcms = (string?)null, cstIcms = "000", aliquotaIcms = 12m,
            cstPisCofins = "02", aliquotaPis = 1.65m, aliquotaCofins = 7.6m, cfop = "5102",
            descricao = "CST 000 — tributado integral. PIS 1,65% / COFINS 7,6% (regime não cumulativo)."
        },
        _ => new { descricao = "Regime não reconhecido." }
    };

    [HttpPost("{id:guid}/csc-nfce")]
    public async Task<IActionResult> ConfigurarCscNFCe(Guid id, [FromBody] CscRequest req, CancellationToken ct)
    {
        var config = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Configuração não encontrada.");
        config.ConfigurarNFCe(req.CscId, req.CscToken);
        repo.Atualizar(config);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarConfiguracaoRequest(
    Guid EmpresaId, string Regime,
    string? CscId = null, string? CscToken = null);

public record AtualizarConfiguracaoRequest(
    string? Regime, string? Ambiente,
    string? CscIdNFCe, string? CscTokenNFCe,
    int? SerieNFe, int? SerieNFCe,
    string? EmailContador, bool? EnviarEmailAposEmissao);

public record CscRequest(string CscId, string CscToken);
