using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Desempenho.Entities;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Desempenho;

namespace Sistema.Infrastructure.Jobs;

/// <summary>Arquiva automaticamente todo mês o demonstrativo de premiação (PDF + valores
/// congelados) de cada colaborador, referente ao mês anterior. Idempotente (upsert).</summary>
public class ArquivarDemonstrativosJob(SistemaDbContext db, PremiacaoCalculoService calc,
    ILogger<ArquivarDemonstrativosJob> logger)
{
    private const string BaseUrl = "https://sistema.ecogranel.com.br";

    /// <summary>Roda mensalmente: arquiva o mês anterior para todas as empresas.</summary>
    public async Task ExecutarAsync()
    {
        var comp = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        var empresas = await db.Empresas.AsNoTracking().Select(e => e.Id).ToListAsync();
        var total = 0;
        foreach (var empresaId in empresas)
            total += await ArquivarAsync(empresaId, comp.Year, comp.Month);
        logger.LogInformation("[Premiação] Arquivados {N} demonstrativos de {Mes:00}/{Ano}.", total, comp.Month, comp.Year);
    }

    /// <summary>Arquiva (ou reprocessa) uma competência de uma empresa. Retorna a quantidade.</summary>
    public async Task<int> ArquivarAsync(Guid empresaId, int ano, int mes)
    {
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId);
        if (empresa is null) return 0;
        var linhas = await calc.CalcularAsync(empresaId, ano, mes, null);
        if (linhas.Count == 0) return 0;

        var existentes = await db.DemonstrativosArquivados
            .Where(x => x.EmpresaId == empresaId && x.Ano == ano && x.Mes == mes).ToListAsync();

        foreach (var linha in linhas)
        {
            var r = linha.Res;
            var url = $"{BaseUrl}/api/premiacao/verificar/premio?empresaId={empresaId}&ano={ano}&mes={mes}&colaboradorId={r.ColaboradorId}";
            var pdf = calc.GerarDemonstrativoPdf(linha, empresa.RazaoSocial ?? "", ano, mes, url);
            var e = existentes.FirstOrDefault(x => x.ColaboradorId == r.ColaboradorId);
            if (e is null)
                db.DemonstrativosArquivados.Add(DemonstrativoArquivado.Criar(empresaId, r.ColaboradorId, r.Colaborador, ano, mes, r.Premio, pdf));
            else
                e.Atualizar(r.Premio, pdf);
        }
        await db.SaveChangesAsync();
        return linhas.Count;
    }
}
