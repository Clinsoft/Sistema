using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QRCoder;
using Sistema.Domain.Desempenho.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Desempenho;

public record MetaResolvida(decimal MetaLoja, decimal MetaIndividual, decimal BaseFaturamento, int Vendedores, bool Manual);
public record PremiacaoLinha(ResultadoPremio Res, string LojaNome, Guid LocalEstoqueId,
    decimal FaturamentoLoja, decimal MetaLoja, decimal PercentLoja, List<AvaliacaoDesempenhoSemanal> Avaliacoes);

/// <summary>Núcleo de cálculo do prêmio e geração do demonstrativo em PDF
/// (compartilhado entre o controller e o job de arquivamento).</summary>
public class PremiacaoCalculoService(SistemaDbContext db)
{
    public async Task<Dictionary<Guid, MetaResolvida>> ResolverMetasAsync(
        Guid empresaId, int ano, int mes, ConfiguracaoPremiacao cfg, CancellationToken ct = default)
    {
        var baseIni = new DateTime(ano, mes, 1).AddMonths(-cfg.MesesBaseMeta);
        var baseFim = new DateTime(ano, mes, 1);

        var fatBase = (await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                && v.DataHora >= baseIni && v.DataHora < baseFim)
            .GroupBy(v => v.LocalEstoqueId)
            .Select(g => new { Loja = g.Key, Total = g.Sum(v => v.Total) }).ToListAsync(ct))
            .ToDictionary(x => x.Loja, x => x.Total);

        var vendBase = (await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada && v.VendedorId != null
                && v.DataHora >= baseIni && v.DataHora < baseFim)
            .Select(v => new { v.LocalEstoqueId, v.VendedorId }).Distinct().ToListAsync(ct))
            .GroupBy(x => x.LocalEstoqueId).ToDictionary(g => g.Key, g => g.Count());

        var overrides = await db.MetasPremiacaoLoja.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Ano == ano && x.Mes == mes)
            .ToDictionaryAsync(x => x.LocalEstoqueId, x => x, ct);

        var lojas = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId).Select(l => l.Id).ToListAsync(ct);

        var dict = new Dictionary<Guid, MetaResolvida>();
        foreach (var loja in lojas)
        {
            var fat = fatBase.TryGetValue(loja, out var f) ? f : 0m;
            var media = Math.Round(fat / cfg.MesesBaseMeta, 2);
            var vend = vendBase.TryGetValue(loja, out var vv) ? vv : 0;
            if (overrides.TryGetValue(loja, out var ov))
                dict[loja] = new MetaResolvida(ov.MetaLoja, ov.MetaIndividual, media, vend, true);
            else
            {
                var metaLoja = Math.Round(media * cfg.FatorMetaLoja / 100m, 2);
                var metaInd = Math.Round(metaLoja / Math.Max(1, vend), 2);
                dict[loja] = new MetaResolvida(metaLoja, metaInd, media, vend, false);
            }
        }
        return dict;
    }

    public async Task<List<PremiacaoLinha>> CalcularAsync(Guid empresaId, int ano, int mes,
        Guid? apenasColaborador, CancellationToken ct = default)
    {
        var cfg = await db.ConfiguracoesPremiacao.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct) ?? ConfiguracaoPremiacao.Padrao(empresaId);
        var inicio = new DateTime(ano, mes, 1);
        var fimExcl = inicio.AddMonths(1);

        var metas = await ResolverMetasAsync(empresaId, ano, mes, cfg, ct);

        var lojas = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId).ToDictionaryAsync(l => l.Id, l => l.Nome, ct);

        var fatLoja = (await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                && v.DataHora >= inicio && v.DataHora < fimExcl)
            .GroupBy(v => v.LocalEstoqueId)
            .Select(g => new { Loja = g.Key, Total = g.Sum(v => v.Total) }).ToListAsync(ct))
            .ToDictionary(x => x.Loja, x => x.Total);

        var vendaVendedor = (await db.Vendas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Status == StatusVenda.Finalizada
                && v.VendedorId != null && v.DataHora >= inicio && v.DataHora < fimExcl)
            .GroupBy(v => v.VendedorId!.Value)
            .Select(g => new { Vend = g.Key, Total = g.Sum(v => v.Total) }).ToListAsync(ct))
            .ToDictionary(x => x.Vend, x => x.Total);

        var avaliacoes = (await db.AvaliacoesDesempenho.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.Ano == ano && a.Mes == mes).ToListAsync(ct))
            .GroupBy(a => a.ColaboradorId).ToDictionary(g => g.Key, g => g.ToList());

        var apuracoes = await db.ApuracoesPremiacao.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.Ano == ano && a.Mes == mes)
            .ToDictionaryAsync(a => a.ColaboradorId, a => a, ct);

        var hoje = DateTime.Today;
        var lojasComVencido = new HashSet<Guid>(await db.Lotes.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Quantidade > 0
                && l.DataValidade != null && l.DataValidade < hoje)
            .Select(l => l.LocalEstoqueId).Distinct().ToListAsync(ct));

        var idsAtividade = new HashSet<Guid>(vendaVendedor.Keys);
        idsAtividade.UnionWith(avaliacoes.Keys);
        idsAtividade.UnionWith(apuracoes.Keys);
        if (apenasColaborador.HasValue)
            idsAtividade = new HashSet<Guid> { apenasColaborador.Value };

        var roster = await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.LocalEstoqueId != null && idsAtividade.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.LocalEstoqueId })
            .ToListAsync(ct);

        var lista = new List<PremiacaoLinha>();
        foreach (var u in roster)
        {
            var loja = u.LocalEstoqueId!.Value;
            var metaTup = metas.TryGetValue(loja, out var mt) ? mt : new MetaResolvida(0, 0, 0, 0, false);
            var metaLoja = metaTup.MetaLoja;
            var metaInd = metaTup.MetaIndividual;
            var valorBaseLoja = cfg.ValorBaseDinamico && metaInd > 0
                ? Math.Round(metaInd * cfg.PercentFaturamentoPremio / 100m, 2)
                : cfg.ValorBase;
            var fat = fatLoja.TryGetValue(loja, out var f) ? f : 0;
            var vendaInd = vendaVendedor.TryGetValue(u.Id, out var vi) ? vi : 0;
            var avalsU = avaliacoes.TryGetValue(u.Id, out var av) ? av : new List<AvaliacaoDesempenhoSemanal>();
            var temVencido = lojasComVencido.Contains(loja);
            decimal perf = 0, descValidade = 0;
            if (avalsU.Count > 0)
            {
                if (temVencido)
                {
                    perf = Math.Round(avalsU.Average(a => Math.Max(0, a.Pontos - 10m * (int)a.Validade / 100m)), 1);
                    descValidade = Math.Round(avalsU.Average(a => 10m * (int)a.Validade / 100m), 1);
                }
                else perf = Math.Round(avalsU.Average(a => a.Pontos), 1);
            }
            var apu = apuracoes.TryGetValue(u.Id, out var ap) ? ap : null;

            var res = CalculoPremiacao.Calcular(u.Id, u.Nome, loja, fat, metaLoja, vendaInd, metaInd,
                perf, avalsU.Count, cfg, valorBaseLoja, apu, descValidade);
            var pctLoja = metaLoja > 0 ? Math.Round(fat / metaLoja * 100, 1) : 0;
            lista.Add(new PremiacaoLinha(res, lojas.TryGetValue(loja, out var ln) ? ln : "—", loja, fat, metaLoja, pctLoja, avalsU));
        }
        return lista;
    }

    public byte[] GerarDemonstrativoPdf(PremiacaoLinha t, string empresaNome, int ano, int mes, string urlVerif)
    {
        var r = t.Res;
        string M(decimal v) => "R$ " + v.ToString("N2", new System.Globalization.CultureInfo("pt-BR"));
        var qr = GerarQrPng(urlVerif);

        var pdf = Document.Create(doc => doc.Page(page =>
        {
            page.Margin(36); page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor("#1b241e"));
            page.Header().Column(h =>
            {
                h.Item().Text("Demonstrativo de Premiação por Desempenho").FontSize(15).Bold().FontColor("#b5852a");
                h.Item().Text($"Competência {mes:00}/{ano} · {empresaNome}").FontSize(10).FontColor("#666");
            });
            page.Content().PaddingVertical(14).Column(c =>
            {
                c.Spacing(6);
                void Linha(string k, string v, bool destaque = false) => c.Item().Row(row =>
                {
                    row.ConstantItem(210).Text(k).FontColor("#555").SemiBold();
                    var span = row.RelativeItem().Text(v);
                    span.FontSize(destaque ? 13 : 10).FontColor(destaque ? "#1e7a46" : "#1b241e");
                    if (destaque) span.Bold();
                });

                Linha("Colaborador(a):", r.Colaborador);
                Linha("Loja:", t.LojaNome);
                c.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor("#e0e0e0");
                Linha("Meta da loja:", $"{M(r.FaturamentoLoja)} de {M(r.MetaLoja)}  ({r.PercentLoja:0.#}%)");
                Linha("Meta individual:", $"{M(r.VendaIndividual)} de {M(r.MetaIndividual)}  ({r.PercentIndividual:0.#}%)");
                Linha("Performance comercial:", $"{r.PerformancePercent:0.#}% ({r.SemanasAvaliadas} semana(s))"
                    + (r.DescontoValidade > 0 ? $"  — desconto validade: -{r.DescontoValidade:0.#}" : ""));
                c.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor("#e0e0e0");
                Linha("Valor base (ativação da loja):", M(r.BaseLoja));
                Linha("Fator individual:", $"{r.FatorIndividual * 100:0}%");
                Linha("Prêmio do mês:", M(r.Premio), destaque: true);
                if (r.Premio == 0 && !string.IsNullOrEmpty(r.Motivo))
                    c.Item().PaddingTop(4).Background("#F6EDD9").Padding(8).Text($"Sem prêmio neste mês: {r.Motivo}").FontSize(9).FontColor("#9a6b18");
                c.Item().PaddingTop(8).Text("Cálculo: Valor base × Fator individual × Performance% (conforme o Regulamento). Documento informativo; o prêmio, quando devido, segue as condições do regulamento.")
                    .FontSize(8).FontColor("#777");
            });
            page.Footer().PaddingTop(8).Row(f =>
            {
                f.ConstantItem(58).Image(qr).FitArea();
                f.RelativeItem().PaddingLeft(8).AlignMiddle().Column(col =>
                {
                    col.Item().Text("Verificação (aponte a câmera)").FontSize(8).SemiBold().FontColor("#555");
                    col.Item().Text($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7).FontColor("#999");
                });
            });
        }));
        return pdf.GeneratePdf();
    }

    public static byte[] GerarQrPng(string texto)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }
}
