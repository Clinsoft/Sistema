using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/configuracao")]
[Authorize(Roles = "Administrador,Contador")]
public class ConfiguracaoFiscalController(
    IConfiguracaoFiscalRepository repo, IUnitOfWork uow) : ControllerBase
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

public record CscRequest(string CscId, string CscToken);
