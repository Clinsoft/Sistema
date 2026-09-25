using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Assinaturas;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Auth;

/// <summary>Limites quantitativos do plano da empresa (usuários/lojas). Sem assinatura = sem limite.</summary>
public static class LimitePlano
{
    public static async Task<(int MaxUsuarios, int MaxLojas)?> ObterAsync(
        SistemaDbContext db, Guid empresaId, CancellationToken ct)
    {
        var a = await db.Assinaturas.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct);
        if (a is null) return null;
        var plano = a.PlanoEfetivo();
        return (PlanoCatalogo.MaxUsuarios(plano), PlanoCatalogo.MaxLojas(plano, a.LojasContratadas));
    }
}
