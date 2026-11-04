using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sistema.Domain.Assinaturas;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers;

/// <summary>Cliente na gestão: assinatura mais os dados da empresa.</summary>
public record ClienteAssinatura(Guid EmpresaId, string Nome, string Cnpj, string? Email, DateTime CriadoEm,
    string Plano, string Status, string Situacao, string Ciclo, DateTime? TrialAte, DateTime? ProximoVencimento,
    int LojasContratadas, int Usuarios, int Lojas);

/// <summary>O que o painel do super-admin e o painel de gestão fazem em comum: listar os clientes e alterar uma assinatura.</summary>
public class GestaoAssinaturas(SistemaDbContext db, IConfiguration config, IMemoryCache cache)
{
    public async Task<List<ClienteAssinatura>> ListarAsync(CancellationToken ct)
    {
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
        return rows.Select(r => new ClienteAssinatura(r.a.EmpresaId, r.NomeFantasia, r.Cnpj, r.Email, r.CriadoEm,
                r.a.Plano.ToString(), r.a.Status.ToString(), r.a.Situacao(agora).ToString(), r.a.Ciclo.ToString(),
                r.a.TrialAte, r.a.ProximoVencimento, r.a.LojasContratadas,
                usuarios.GetValueOrDefault(r.a.EmpresaId), lojas.GetValueOrDefault(r.a.EmpresaId)))
            .OrderByDescending(x => x.CriadoEm)
            .ToList();
    }

    /// <summary>Carrega a assinatura, aplica a mudança, salva e invalida o cache do gate. Null se a empresa não tem assinatura.</summary>
    public async Task<Assinatura?> AplicarAsync(Guid empresaId, Action<Assinatura> acao, CancellationToken ct)
    {
        var a = await db.Assinaturas.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct);
        if (a is null) return null;
        acao(a);
        await db.SaveChangesAsync(ct);
        cache.Remove($"assinatura:{empresaId}");   // reflete na hora no gate
        return a;
    }

    public Task<string?> NomeAsync(Guid empresaId, CancellationToken ct)
        => db.Empresas.AsNoTracking().Where(e => e.Id == empresaId).Select(e => (string?)e.NomeFantasia).FirstOrDefaultAsync(ct);
}
