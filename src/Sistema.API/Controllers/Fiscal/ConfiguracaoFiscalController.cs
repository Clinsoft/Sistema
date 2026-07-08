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
        if (config is null) return NotFound("Configuração fiscal não encontrada.");
        // Projeta enums como string e corrige os nomes de campo esperados pelo frontend
        return Ok(new
        {
            config.Id, config.EmpresaId,
            regime = config.Regime.ToString(),
            ambiente = config.Ambiente.ToString(),
            config.SerieNFe, config.SerieNFCe,
            proximoNumeroNFe = config.ProximoNumerNFe,
            proximoNumeroNFCe = config.ProximoNumerNFCe,
            cscIdNFCe = config.CscIdNFCe,
            cscTokenNFCe = config.CscTokenNFCe,
            config.EmailContador, config.EnviarEmailAposEmissao,
            // Parâmetros gerais
            config.NaturezaOperacaoPadrao, config.ContingenciaPadrao,
            config.FormatoDanfe, config.TipoImpressaoNFCe, config.ImprimirAutomaticamenteNFCe,
            // Tributação padrão
            config.CsosnPadrao, config.CstIcmsPadrao, config.AliquotaIcmsPadrao,
            config.AliquotaIcmsInterestadual, config.OrigemPadrao,
            config.CstPisPadrao, config.AliquotaPisPadrao,
            config.CstCofinsPadrao, config.AliquotaCofinsPadrao,
            config.CfopVendaEstadual, config.CfopVendaInterestadual, config.CfopVendaConsumidor,
            // NFS-e
            config.HabilitarNFSe, config.InscricaoMunicipal, config.CodigoMunicipioIbge,
            config.SerieNFSe, config.RegimeEspecialTributacao, config.CodigoServicoMunicipalPadrao,
            config.AliquotaIssPadrao, config.IssRetidoFonte, config.IncentivadorCultural,
            // MDF-e
            config.HabilitarMDFe, config.SerieMDFe, config.ProximoNumeroMDFe,
            config.TipoEmitenteMDFe, config.ModalTransporteMDFe, config.Rntrc,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarConfiguracaoRequest req, CancellationToken ct)
    {
        var existe = await repo.ObterPorEmpresaAsync(req.EmpresaId, ct);
        if (existe is not null)
            throw new InvalidOperationException("Configuração fiscal já existe para esta empresa.");

        var regime = Enum.Parse<RegimeTributario>(req.Regime);
        var config = ConfiguracaoFiscal.Criar(req.EmpresaId, regime);

        var cscId = req.CscId ?? req.CscIdNFCe;
        var cscToken = req.CscToken ?? req.CscTokenNFCe;
        if (!string.IsNullOrEmpty(cscId) && !string.IsNullOrEmpty(cscToken))
            config.ConfigurarNFCe(cscId, cscToken);

        config.DefinirSeriesENumeracao(req.SerieNFe, req.SerieNFCe,
            req.ProximoNumeroNFe, req.ProximoNumeroNFCe);
        config.DefinirEmail(req.EmailCopiaFixa, req.EnviarEmailDestinatario ?? false);

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

        // Séries e próxima numeração de NF-e / NFC-e
        config.DefinirSeriesENumeracao(
            Int(body, "serieNFe"), Int(body, "serieNFCe"),
            Long(body, "proximoNumeroNFe"), Long(body, "proximoNumeroNFCe"));

        // E-mail do contador (cópia fixa) + envio automático
        if (body.TryGetProperty("emailCopiaFixa", out var emailProp) ||
            body.TryGetProperty("enviarEmailDestinatario", out _))
        {
            var email = body.TryGetProperty("emailCopiaFixa", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String
                ? e.GetString() : config.EmailContador;
            var enviar = body.TryGetProperty("enviarEmailDestinatario", out var ev) &&
                ev.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
                ? ev.GetBoolean() : config.EnviarEmailAposEmissao;
            config.DefinirEmail(email, enviar);
        }

        // Parâmetros gerais de documentos
        config.DefinirParametrosDocumentos(
            Str(body, "naturezaOperacaoPadrao"), Str(body, "contingenciaPadrao"),
            Str(body, "formatoDanfe"), Str(body, "tipoImpressaoNFCe"),
            Bool(body, "imprimirAutomaticamenteNFCe") ?? config.ImprimirAutomaticamenteNFCe);

        // Tributação padrão de produtos
        config.DefinirTributacaoPadrao(
            Str(body, "csosnPadrao"), Str(body, "cstIcmsPadrao"),
            Dec(body, "aliquotaIcmsPadrao") ?? config.AliquotaIcmsPadrao,
            Dec(body, "aliquotaIcmsInterestadual") ?? config.AliquotaIcmsInterestadual,
            Str(body, "origemPadrao"),
            Str(body, "cstPisPadrao"), Dec(body, "aliquotaPisPadrao") ?? config.AliquotaPisPadrao,
            Str(body, "cstCofinsPadrao"), Dec(body, "aliquotaCofinsPadrao") ?? config.AliquotaCofinsPadrao,
            Str(body, "cfopVendaEstadual"), Str(body, "cfopVendaInterestadual"), Str(body, "cfopVendaConsumidor"));

        // NFS-e
        config.DefinirNFSe(
            Bool(body, "habilitarNFSe") ?? config.HabilitarNFSe,
            Str(body, "inscricaoMunicipal"), Str(body, "codigoMunicipioIbge"),
            Int(body, "serieNFSe") ?? config.SerieNFSe,
            Str(body, "regimeEspecialTributacao"), Str(body, "codigoServicoMunicipalPadrao"),
            Dec(body, "aliquotaIssPadrao") ?? config.AliquotaIssPadrao,
            Bool(body, "issRetidoFonte") ?? config.IssRetidoFonte,
            Bool(body, "incentivadorCultural") ?? config.IncentivadorCultural);

        // MDF-e
        config.DefinirMDFe(
            Bool(body, "habilitarMDFe") ?? config.HabilitarMDFe,
            Int(body, "serieMDFe") ?? config.SerieMDFe,
            Long(body, "proximoNumeroMDFe") ?? config.ProximoNumeroMDFe,
            Str(body, "tipoEmitenteMDFe"), Str(body, "modalTransporteMDFe"), Str(body, "rntrc"));

        repo.Atualizar(config);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // Lê um inteiro do JSON (aceita number ou string numérica); null se ausente/ inválido.
    private static int? Int(System.Text.Json.JsonElement body, string nome)
    {
        if (!body.TryGetProperty(nome, out var p)) return null;
        if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
        if (p.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    private static long? Long(System.Text.Json.JsonElement body, string nome)
    {
        if (!body.TryGetProperty(nome, out var p)) return null;
        if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetInt64(out var n)) return n;
        if (p.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    // Lê string do JSON; null (não vazio) quando ausente, para não sobrescrever com "".
    private static string? Str(System.Text.Json.JsonElement body, string nome)
    {
        if (!body.TryGetProperty(nome, out var p)) return null;
        if (p.ValueKind == System.Text.Json.JsonValueKind.String) return p.GetString();
        if (p.ValueKind == System.Text.Json.JsonValueKind.Number) return p.ToString();
        return null;
    }

    private static decimal? Dec(System.Text.Json.JsonElement body, string nome)
    {
        if (!body.TryGetProperty(nome, out var p)) return null;
        if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetDecimal(out var n)) return n;
        if (p.ValueKind == System.Text.Json.JsonValueKind.String &&
            decimal.TryParse(p.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    private static bool? Bool(System.Text.Json.JsonElement body, string nome)
    {
        if (!body.TryGetProperty(nome, out var p)) return null;
        if (p.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
            return p.GetBoolean();
        return null;
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
    string? CscId = null, string? CscToken = null,
    string? CscIdNFCe = null, string? CscTokenNFCe = null,
    int? SerieNFe = null, int? SerieNFCe = null,
    long? ProximoNumeroNFe = null, long? ProximoNumeroNFCe = null,
    string? EmailCopiaFixa = null, bool? EnviarEmailDestinatario = null);

public record AtualizarConfiguracaoRequest(
    string? Regime, string? Ambiente,
    string? CscIdNFCe, string? CscTokenNFCe,
    int? SerieNFe, int? SerieNFCe,
    string? EmailContador, bool? EnviarEmailAposEmissao);

public record CscRequest(string CscId, string CscToken);
