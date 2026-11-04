using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Assinaturas;

namespace Sistema.API.Controllers;

/// <summary>
/// Painel SuperAdmin (control plane do SaaS): gestão das assinaturas de TODAS as lojas-cliente.
/// Cross-tenant — por isso os parâmetros de empresa se chamam <c>id</c> (não "empresaId"), para
/// não cair no IsolamentoEmpresaFilter. Marcado [PermitirBloqueado] para o gate não travar caso
/// o próprio super-admin tenha um tenant vencido. Acesso restrito por e-mail (config SuperAdmin:Emails).
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
[Sistema.API.Auth.PermitirBloqueado]
public class AdminController(GestaoAssinaturas gestao, IConfiguration config) : ControllerBase
{
    private bool EhSuperAdmin()
    {
        var email = (User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email)) return false;
        var lista = (config["SuperAdmin:Emails"] ?? "")
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lista.Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));
    }

    [HttpGet("eu")]
    public IActionResult Eu() => Ok(new { superAdmin = EhSuperAdmin() });

    /// <summary>Roda a manutenção das assinaturas sob demanda: bloqueio + régua de lembretes.</summary>
    [HttpPost("rodar-bloqueio")]
    public IActionResult RodarBloqueio()
    {
        if (!EhSuperAdmin()) return Forbid();
        Hangfire.BackgroundJob.Enqueue<Sistema.Infrastructure.Jobs.AssinaturaStatusJob>(j => j.ExecutarAsync());
        Hangfire.BackgroundJob.Enqueue<Sistema.Infrastructure.Jobs.AssinaturaLembreteJob>(j => j.ExecutarAsync());
        return Ok(new { ok = true, mensagem = "Manutenção (bloqueio + lembretes) enfileirada." });
    }

    [HttpGet("empresas")]
    public async Task<IActionResult> Empresas(CancellationToken ct)
    {
        if (!EhSuperAdmin()) return Forbid();
        var lista = await gestao.ListarAsync(ct);
        return Ok(new { total = lista.Count, empresas = lista });
    }

    [HttpPost("{id:guid}/plano")]
    public Task<IActionResult> TrocarPlano(Guid id, [FromBody] TrocarPlanoDto dto, CancellationToken ct)
        => Aplicar(id, ct, a =>
        {
            if (!Enum.TryParse<PlanoAssinatura>(dto.Plano, true, out var p)) throw new ArgumentException("Plano inválido.");
            a.TrocarPlano(p, dto.LojasContratadas);
        });

    [HttpPost("{id:guid}/pagar")]
    public Task<IActionResult> RegistrarPagamento(Guid id, [FromBody] PagarDto dto, CancellationToken ct)
        => Aplicar(id, ct, a =>
        {
            var venc = dto.ProximoVencimento ?? DateTime.UtcNow.AddMonths(a.Ciclo == CicloCobranca.Anual ? 12 : 1);
            a.RegistrarPagamento(venc);
        });

    [HttpPost("{id:guid}/bloquear")]
    public Task<IActionResult> Bloquear(Guid id, [FromBody] MotivoDto? dto, CancellationToken ct)
        => Aplicar(id, ct, a => a.Bloquear(dto?.Motivo));

    [HttpPost("{id:guid}/reativar")]
    public Task<IActionResult> Reativar(Guid id, CancellationToken ct)
        => Aplicar(id, ct, a => a.Reativar());

    [HttpPost("{id:guid}/trial")]
    public Task<IActionResult> EstenderTrial(Guid id, [FromBody] DiasDto dto, CancellationToken ct)
        => Aplicar(id, ct, a => a.EstenderTrial(dto.Dias <= 0 ? 7 : dto.Dias));

    [HttpPost("{id:guid}/ciclo")]
    public Task<IActionResult> Ciclo(Guid id, [FromBody] CicloDto dto, CancellationToken ct)
        => Aplicar(id, ct, a =>
        {
            if (!Enum.TryParse<CicloCobranca>(dto.Ciclo, true, out var c)) throw new ArgumentException("Ciclo inválido.");
            a.DefinirCiclo(c);
        });

    private async Task<IActionResult> Aplicar(Guid empresaId, CancellationToken ct, Action<Assinatura> acao)
    {
        if (!EhSuperAdmin()) return Forbid();
        try
        {
            var a = await gestao.AplicarAsync(empresaId, acao, ct);
            if (a is null) return NotFound(new { mensagem = "Empresa sem assinatura." });
            return Ok(new { ok = true, status = a.Status.ToString(), situacao = a.Situacao().ToString(), plano = a.Plano.ToString() });
        }
        catch (ArgumentException ex) { return BadRequest(new { mensagem = ex.Message }); }
    }
}

public record TrocarPlanoDto(string Plano, int? LojasContratadas = null);
public record PagarDto(DateTime? ProximoVencimento = null);
public record MotivoDto(string? Motivo = null);
public record DiasDto(int Dias = 7);
public record CicloDto(string Ciclo);
