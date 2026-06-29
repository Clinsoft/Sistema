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

        foreach (var lote in lotes)
        {
            if (lote.DataValidade is null) continue;

            var dias = (lote.DataValidade.Value.Date - hoje).Days;
            var nivel = ClassificarNivel(dias, cfg);
            if (nivel is null) continue;

            // Verifica se já gerou alerta deste nível para este lote hoje
            var jaAlertado = alertasHoje.Any(a =>
                a.LoteId == lote.Id && a.Nivel == nivel);

            if (jaAlertado) continue;

            // Registra o alerta
            var alerta = AlertaValidade.Criar(empresaId, lote.ProdutoId, lote.Id,
                lote.DataValidade.Value, nivel);
            db.AlertasValidade.Add(alerta);

            switch (nivel)
            {
                case "Amarelo": totalAmarelo++; break;
                case "Vermelho": totalVermelho++; break;
                case "Urgente":  totalUrgente++;  break;
                case "Vencido":  totalVencido++;  break;
            }

            // Promoção automática ao atingir nível vermelho
            if (nivel == "Vermelho" && cfg.PromoAutomatica && !cfg.ExigeAprovacao)
            {
                var precoOriginal = lote.Produto.PrecoVenda;
                var precoPromo    = Math.Round(precoOriginal * (1 - cfg.DescontoAutoPercent / 100), 2);

                var baseLayout = new
                {
                    gerador       = "ValidadeAutomatica",
                    nomePromocao  = $"OFERTA — {lote.Produto.Descricao}",
                    desconto      = cfg.DescontoAutoPercent,
                    tipoDesconto  = "Percentual",
                    tipoPromocao  = "Desconto",
                    aplicaEm      = "ProdutoEspecifico",
                    produtoId     = lote.ProdutoId,
                    produtoNome   = lote.Produto.Descricao,
                    precoOriginal,
                    precoPromo,
                    dataValidade  = lote.DataValidade.Value.ToString("dd/MM/yyyy"),
                    diasRestantes = dias,
                    dataInicio    = hoje.ToString("yyyy-MM-dd"),
                    dataFim       = lote.DataValidade.Value.ToString("yyyy-MM-dd"),
                    apenasClube   = false,
                };

                // Gera artes para Feed, Story e Banner
                Guid primeiraArteId = Guid.Empty;
                foreach (var (formato, label) in new[] {
                    (FormatoArte.FeedQuadrado,    "Feed"),
                    (FormatoArte.StoryVertical,   "Story"),
                    (FormatoArte.BannerHorizontal,"Banner"),
                })
                {
                    var layoutJson = JsonSerializer.Serialize(
                        new { baseLayout.gerador, formato = formato.ToString(),
                              baseLayout.nomePromocao, baseLayout.desconto, baseLayout.tipoDesconto,
                              baseLayout.tipoPromocao, baseLayout.aplicaEm, baseLayout.produtoId,
                              baseLayout.produtoNome, baseLayout.precoOriginal, baseLayout.precoPromo,
                              baseLayout.dataValidade, baseLayout.diasRestantes,
                              baseLayout.dataInicio, baseLayout.dataFim, baseLayout.apenasClube });

                    var arte = ArteMarketing.Criar(
                        empresaId,
                        $"⚠️ {lote.Produto.Descricao} — {label} ({dias}d)",
                        TipoArteMarketing.Promocao,
                        formato,
                        layoutJson);

                    db.ArtesMarketing.Add(arte);
                    if (label == "Feed") primeiraArteId = arte.Id;
                }

                if (primeiraArteId != Guid.Empty)
                    alerta.MarcarPromoGerada(primeiraArteId);

                totalPromos++;
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
