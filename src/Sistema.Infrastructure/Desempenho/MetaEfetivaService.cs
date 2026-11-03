using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Desempenho;

/// <summary>
/// Meta EFETIVA = reajuste dinâmico "sobe se bater", calculado NA HORA (sem persistir),
/// sobre o plano salvo (MetasVendaMensal) — o plano fica intocado e o reajuste é
/// recomputado a cada leitura (idempotente e reversível).
///
/// Regra (decidida com o dono): PROPORCIONAL/SAZONAL, encadeada.
/// Para o mês-alvo, olha os meses JÁ FECHADOS do mesmo ano (1..mesAlvo-1). Se algum fechou
/// acima da própria meta, pega o MAIOR "quanto bateu" (ratio = realizado/meta) e multiplica
/// a meta sazonal do mês-alvo por esse fator. Como multiplica a meta de CADA mês (que já
/// carrega a sazonalidade), janeiro continua baixo relativo a dezembro — não "estoura".
/// Teto de segurança para um reajuste não disparar.
/// </summary>
public class MetaEfetivaService(SistemaDbContext db)
{
    /// <summary>Teto do fator de reajuste (no máximo +10%).</summary>
    public const decimal TetoReajuste = 1.10m;

    /// <summary>Vigência: o reajuste só vale para meses a partir de Nov/2026. Meses anteriores
    /// (premiação já fechada) NÃO são reajustados — protege o que já foi trabalhado/pago.</summary>
    public const int VigenciaAno = 2026;
    public const int VigenciaMes = 11;

    /// <summary>Fator de reajuste (>= 1) por loja para o mês-alvo, a partir dos meses
    /// fechados anteriores (mesmo ano) que bateram a própria meta. Vazio/1 = sem reajuste.</summary>
    public async Task<Dictionary<Guid, decimal>> FatoresAsync(
        Guid empresaId, int ano, int mesAlvo, CancellationToken ct = default)
    {
        var fatores = new Dictionary<Guid, decimal>();
        if (mesAlvo <= 1) return fatores;   // janeiro não tem mês fechado anterior no ano
        // Só pra frente: antes da vigência (Nov/2026) não reajusta (não mexe em mês já fechado).
        if (ano < VigenciaAno || (ano == VigenciaAno && mesAlvo < VigenciaMes)) return fatores;

        var ini = new DateTime(ano, 1, 1);
        var fimEx = new DateTime(ano, mesAlvo, 1);   // exclui o mês-alvo (só meses fechados)

        // Plano salvo dos meses fechados (por loja/mês)
        var plano = (await db.MetasVendaMensal.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Ano == ano && x.Mes < mesAlvo && x.LocalEstoqueId != null)
            .Select(x => new { Loja = x.LocalEstoqueId!.Value, x.Mes, x.Valor })
            .ToListAsync(ct))
            .GroupBy(x => (x.Loja, x.Mes))
            .ToDictionary(g => g.Key, g => g.Last().Valor);
        if (plano.Count == 0) return fatores;

        // Realizado = MAIOR entre venda registrada e faturamento histórico importado do mês.
        var vendas = (await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                && v.DataHora >= ini && v.DataHora < fimEx)
            .Select(v => new { Loja = v.LocalEstoqueId, v.DataHora, v.Total })
            .ToListAsync(ct))
            .GroupBy(v => (v.Loja, v.DataHora.Month))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

        var hist = (await db.HistoricoFaturamentoLoja.AsNoTracking()
            .Where(h => h.EmpresaId == empresaId && h.Ano == ano && h.Mes < mesAlvo)
            .Select(h => new { Loja = h.LocalEstoqueId, h.Mes, h.Faturamento })
            .ToListAsync(ct))
            .GroupBy(h => (h.Loja, h.Mes))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Faturamento));

        foreach (var ((loja, mes), metaMes) in plano)
        {
            if (metaMes <= 0) continue;
            var realVenda = vendas.GetValueOrDefault((loja, mes), 0m);
            var realHist = hist.GetValueOrDefault((loja, mes), 0m);
            var realizado = Math.Max(realVenda, realHist);
            if (realizado <= metaMes) continue;               // não bateu → não reajusta

            var ratio = realizado / metaMes;                  // quanto bateu
            var atual = fatores.GetValueOrDefault(loja, 1m);
            if (ratio > atual) fatores[loja] = ratio;         // encadeado = maior "beat" vence
        }

        // Teto de segurança
        foreach (var loja in fatores.Keys.ToList())
            fatores[loja] = Math.Min(fatores[loja], TetoReajuste);

        return fatores;
    }

    /// <summary>Aplica o fator da loja à meta do plano (nunca abaixo do plano).</summary>
    public static decimal Aplicar(decimal metaPlano, decimal fator)
        => metaPlano <= 0 ? metaPlano : Math.Max(metaPlano, Math.Round(metaPlano * fator, 2));
}
