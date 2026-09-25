using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Assinaturas;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers;

/// <summary>
/// Assinatura da empresa logada — o frontend usa para montar menu/rotas (recursos liberados
/// por plano) e para bloquear o sistema quando a assinatura não está em dia.
/// </summary>
[ApiController]
[Route("api/minha-assinatura")]
[Authorize]
[Sistema.API.Auth.PermitirBloqueado]   // precisa abrir mesmo com assinatura bloqueada (tela de regularização)
public class AssinaturasController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Minha(CancellationToken ct)
    {
        var empresaId = EmpresaClaim();
        if (empresaId is null) return Unauthorized();

        var a = await db.Assinaturas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId.Value, ct);

        // Empresa sem assinatura (instalações antigas / EcoGranel) = acesso TOTAL, sem gate.
        if (a is null)
        {
            return Ok(new
            {
                temAssinatura = false,
                podeUsar = true,
                situacao = "Ativa",
                plano = "Ilimitado",
                planoEfetivo = "Ilimitado",
                status = "Ativa",
                recursos = Enum.GetNames<Recurso>(),
                limites = new { maxUsuarios = PlanoCatalogo.Ilimitado, maxLojas = PlanoCatalogo.Ilimitado },
                trialAte = (DateTime?)null,
                diasRestantesTrial = 0,
                proximoVencimento = (DateTime?)null,
            });
        }

        var agora = DateTime.UtcNow;
        var situacao = a.Situacao(agora);
        var planoEfetivo = a.PlanoEfetivo(agora);
        var diasTrial = a.Status == StatusAssinatura.Trial && a.TrialAte is DateTime t
            ? Math.Max(0, (int)Math.Ceiling((t - agora).TotalDays)) : 0;

        return Ok(new
        {
            temAssinatura = true,
            podeUsar = a.PodeUsar(agora),
            situacao = situacao.ToString(),
            plano = a.Plano.ToString(),
            planoEfetivo = planoEfetivo.ToString(),
            status = a.Status.ToString(),
            ciclo = a.Ciclo.ToString(),
            recursos = a.RecursosEfetivos(agora).Select(r => r.ToString()).ToArray(),
            limites = new
            {
                maxUsuarios = PlanoCatalogo.MaxUsuarios(planoEfetivo),
                maxLojas = PlanoCatalogo.MaxLojas(planoEfetivo, a.LojasContratadas),
            },
            trialAte = a.TrialAte,
            diasRestantesTrial = diasTrial,
            proximoVencimento = a.ProximoVencimento,
        });
    }

    private Guid? EmpresaClaim()
        => Guid.TryParse(User.FindFirst("empresaId")?.Value, out var id) ? id : null;
}
