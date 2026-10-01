using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sistema.Domain.Assinaturas;
using Sistema.Infrastructure.Data;

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
public class AdminController(
    SistemaDbContext db,
    IConfiguration config,
    IMemoryCache cache,
    Sistema.Infrastructure.Services.NfseClientesService nfse) : ControllerBase
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

        // Empresas dos próprios super-admins não são "clientes" — não entram na lista.
        var emailsAdmin = (config["SuperAdmin:Emails"] ?? "")
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var empresasAdmin = await db.Usuarios.AsNoTracking()
            .Where(u => u.Email != null && emailsAdmin.Contains(u.Email))
            .Select(u => u.EmpresaId).Distinct().ToListAsync(ct);

        var rows = await (from a in db.Assinaturas.AsNoTracking()
                          join e in db.Empresas.AsNoTracking() on a.EmpresaId equals e.Id
                          where !empresasAdmin.Contains(a.EmpresaId)
                          select new { a, e.NomeFantasia, e.Cnpj, e.Email, e.CriadoEm }).ToListAsync(ct);

        var usuarios = await db.Usuarios.AsNoTracking()
            .Where(u => u.Email != null && u.SenhaHash != null && u.Ativo)
            .GroupBy(u => u.EmpresaId).Select(g => new { g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var lojas = await db.LocaisEstoque.AsNoTracking().Where(l => l.Ativo)
            .GroupBy(l => l.EmpresaId).Select(g => new { g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.C, ct);

        var agora = DateTime.UtcNow;
        var lista = rows.Select(r => new
        {
            empresaId = r.a.EmpresaId,
            nome = r.NomeFantasia,
            cnpj = r.Cnpj,
            email = r.Email,
            criadoEm = r.CriadoEm,
            plano = r.a.Plano.ToString(),
            status = r.a.Status.ToString(),
            situacao = r.a.Situacao(agora).ToString(),
            ciclo = r.a.Ciclo.ToString(),
            trialAte = r.a.TrialAte,
            proximoVencimento = r.a.ProximoVencimento,
            lojasContratadas = r.a.LojasContratadas,
            usuarios = usuarios.GetValueOrDefault(r.a.EmpresaId),
            lojas = lojas.GetValueOrDefault(r.a.EmpresaId),
        })
        .OrderByDescending(x => x.criadoEm)
        .ToList();

        return Ok(new { total = lista.Count, empresas = lista });
    }

    /// <summary>
    /// Clientes do Emissor NFS-e (app PHP separado), para o painel juntar com as lojas
    /// da Natural POR CNPJ. Leitura via NfseClientesService; se o emissor estiver fora do
    /// ar, devolve ok=false + lista vazia (o painel continua mostrando os dados da Natural).
    /// </summary>
    [HttpGet("nfse-clientes")]
    public async Task<IActionResult> NfseClientes(CancellationToken ct)
    {
        if (!EhSuperAdmin()) return Forbid();
        var r = await nfse.ListarAsync(ct);
        return Ok(new { ok = r.Ok, configurado = nfse.Configurado, erro = r.Erro, total = r.Clientes.Count, clientes = r.Clientes });
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

    // Carrega a assinatura, aplica a mutação, salva e invalida o cache do gate.
    private async Task<IActionResult> Aplicar(Guid empresaId, CancellationToken ct, Action<Assinatura> acao)
    {
        if (!EhSuperAdmin()) return Forbid();
        var a = await db.Assinaturas.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct);
        if (a is null) return NotFound(new { mensagem = "Empresa sem assinatura." });
        try { acao(a); }
        catch (ArgumentException ex) { return BadRequest(new { mensagem = ex.Message }); }
        await db.SaveChangesAsync(ct);
        cache.Remove($"assinatura:{empresaId}");   // reflete na hora no gate
        return Ok(new { ok = true, status = a.Status.ToString(), situacao = a.Situacao().ToString(), plano = a.Plano.ToString() });
    }
}

public record TrocarPlanoDto(string Plano, int? LojasContratadas = null);
public record PagarDto(DateTime? ProximoVencimento = null);
public record MotivoDto(string? Motivo = null);
public record DiasDto(int Dias = 7);
public record CicloDto(string Ciclo);
