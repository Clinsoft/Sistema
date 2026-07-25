using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Marketing.Entities;
using Sistema.Infrastructure.Data;
using System.Text.Json;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Executado diariamente às 8h. Identifica produtos próximos ao vencimento,
/// aplica regras de cor, gera promoções automáticas e artes de divulgação.
/// </summary>
public class ValidadeJob(SistemaDbContext db, ILogger<ValidadeJob> logger)
{
    // Janela de geração de promoção automática (dias até o vencimento).
    private const int PromoDiasMin = 2;
    private const int PromoDiasMax = 45;


    [AutomaticRetry(Attempts = 2)]
    public async Task ExecutarAsync()
    {
        var empresas = await db.Empresas.AsNoTracking()
            .Where(e => e.Ativo)
            .Select(e => new { e.Id, e.NomeFantasia })
            .ToListAsync();

        foreach (var empresa in empresas)
            await ProcessarEmpresa(empresa.Id, empresa.NomeFantasia);
    }

    private async Task ProcessarEmpresa(Guid empresaId, string nomeFantasia)
    {
        // Carrega configuração (ou usa padrão)
        var cfg = await db.ConfiguracoesValidade.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId)
            ?? ConfiguracaoValidade.Padrao(empresaId);

        var hoje = DateTime.Today;

        // Busca todos os lotes com validade definida, não zerados
        var lotes = await (
            from l in db.Lotes
            join p in db.Produtos on l.ProdutoId equals p.Id
            where l.EmpresaId == empresaId
               && l.DataValidade.HasValue
               && l.Quantidade > 0
               && p.Ativo
            select new
            {
                l.Id, l.ProdutoId, l.DataValidade, l.Quantidade,
                Produto = new { p.Descricao, p.PrecoVenda, p.CategoriaId }
            }
        ).AsNoTracking().ToListAsync();

        // Alerta já processados hoje para este nível (evita duplicar)
        var alertasHoje = await db.AlertasValidade
            .Where(a => a.EmpresaId == empresaId && a.ProcessadoEm.Date == hoje)
            .ToListAsync();

        int totalAmarelo = 0, totalVermelho = 0, totalUrgente = 0, totalVencido = 0;
        int totalPromos = 0;
        var promoPorProduto = new Dictionary<Guid, Guid>();

        foreach (var lote in lotes)
        {
            if (lote.DataValidade is null) continue;

            var dias = (lote.DataValidade.Value.Date - hoje).Days;

            // ── Promoção automática: produtos de 2 a 45 dias do vencimento ──
            // Cria uma PROMOÇÃO (módulo Promoções) — não gera arte. A arte fica
            // a critério do administrador.
            if (dias >= PromoDiasMin && dias <= PromoDiasMax
                && cfg.PromoAutomatica && !cfg.ExigeAprovacao
                && !promoPorProduto.ContainsKey(lote.ProdutoId))
            {
                // Evita duplicar: só cria se ainda não há promoção ativa vigente.
                var jaTemPromo = await db.Promocoes.AnyAsync(pr =>
                    pr.EmpresaId == empresaId && pr.Ativa
                    && pr.ReferenciaId == lote.ProdutoId
                    && (pr.DataFim == null || pr.DataFim >= hoje));

                if (!jaTemPromo)
                {
                    var promo = Promocao.Criar(
                        empresaId,
                        $"OFERTA — {lote.Produto.Descricao}",
                        "Desconto", "Percentual", cfg.DescontoAutoPercent,
                        hoje, lote.DataValidade.Value.Date,
                        "Produto", lote.ProdutoId,
                        0, 0, 0, 0, apenasClube: false, cumulativo: false);

                    db.Promocoes.Add(promo);
                    promoPorProduto[lote.ProdutoId] = promo.Id;
                    totalPromos++;
                }
            }

            // ── Classificação de cor para o painel/alertas ──
            var nivel = ClassificarNivel(dias, cfg);
            if (nivel is null) continue;

            // Verifica se já gerou alerta deste nível para este lote hoje
            var jaAlertado = alertasHoje.Any(a =>
                a.LoteId == lote.Id && a.Nivel == nivel);

            if (jaAlertado) continue;

            // Registra o alerta
            var alerta = AlertaValidade.Criar(empresaId, lote.ProdutoId, lote.Id,
                lote.DataValidade.Value, nivel);
            if (promoPorProduto.TryGetValue(lote.ProdutoId, out var promoId))
                alerta.MarcarPromoGerada(promoId);
            db.AlertasValidade.Add(alerta);

            switch (nivel)
            {
                case "Amarelo": totalAmarelo++; break;
                case "Vermelho": totalVermelho++; break;
                case "Urgente":  totalUrgente++;  break;
                case "Vencido":  totalVencido++;  break;
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "[VALIDADE] {Empresa}: 🟡 {Am} amarelo | 🔴 {Ve} vermelho | ⚠️ {Ur} urgente | ✖ {Vn} vencido | {Pr} promos geradas",
            nomeFantasia, totalAmarelo, totalVermelho, totalUrgente, totalVencido, totalPromos);
    }

    private static string? ClassificarNivel(int dias, ConfiguracaoValidade cfg)
    {
        if (dias < 0)                          return "Vencido";
        if (dias <= cfg.DiasAlertaUrgente)     return "Urgente";
        if (dias <= cfg.DiasAlertaVermelho)    return "Vermelho";
        if (dias <= cfg.DiasAlertaAmarelo)     return "Amarelo";
        return null;
    }
}
