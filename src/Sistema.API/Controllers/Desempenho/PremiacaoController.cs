using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;
using Sistema.Domain.Desempenho.Entities;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Desempenho;

/// <summary>Premiação por Desempenho: metas, avaliação semanal, apuração e cálculo do prêmio.</summary>
[ApiController]
[Route("api/premiacao")]
[Authorize]
public class PremiacaoController(SistemaDbContext db) : ControllerBase
{
    private Guid UsuarioId =>
        Guid.TryParse(User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    // ── Configuração geral ────────────────────────────────────────────────
    [HttpGet("config")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> ObterConfig([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesPremiacao.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct) ?? ConfiguracaoPremiacao.Padrao(empresaId);
        var a = ano ?? DateTime.Today.Year;
        var m = mes ?? DateTime.Today.Month;
        var metas = await ResolverMetasAsync(empresaId, a, m, cfg, ct);
        var lojas = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId).Select(l => new { l.Id, l.Nome }).ToListAsync(ct);
        var baseIni = new DateTime(a, m, 1).AddMonths(-cfg.MesesBaseMeta);
        return Ok(new
        {
            cfg.ValorBase, cfg.RedutorPercent, cfg.MinPresenca, cfg.ThresholdLoja, cfg.ThresholdIndividual,
            cfg.FatorMetaLoja, cfg.MesesBaseMeta, cfg.ValorBaseDinamico, cfg.PercentFaturamentoPremio, cfg.Ativo,
            baseInicio = baseIni.ToString("yyyy-MM"), baseFim = new DateTime(a, m, 1).AddMonths(-1).ToString("yyyy-MM"),
            metas = lojas.Select(l =>
            {
                var mt = metas.TryGetValue(l.Id, out var v) ? v : default;
                var valorBase = cfg.ValorBaseDinamico && mt.MetaIndividual > 0
                    ? Math.Round(mt.MetaIndividual * cfg.PercentFaturamentoPremio / 100m, 2)
                    : cfg.ValorBase;
                return new
                {
                    localEstoqueId = l.Id, loja = l.Nome,
                    metaLoja = mt.MetaLoja, metaIndividual = mt.MetaIndividual,
                    baseFaturamento = mt.BaseFaturamento, vendedores = mt.Vendedores, manual = mt.Manual,
                    valorBase
                };
            })
        });
    }

    [HttpPut("config")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> SalvarConfig([FromBody] ConfigPremiacaoRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesPremiacao.FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);
        if (cfg is null) { cfg = ConfiguracaoPremiacao.Padrao(req.EmpresaId); db.ConfiguracoesPremiacao.Add(cfg); }
        cfg.Atualizar(req.ValorBase, req.RedutorPercent, req.MinPresenca, req.ThresholdLoja, req.ThresholdIndividual,
            req.FatorMetaLoja, req.MesesBaseMeta, req.ValorBaseDinamico, req.PercentFaturamentoPremio, req.Ativo);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Override manual da meta de uma loja para uma competência (ano/mês).</summary>
    [HttpPut("metas")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> SalvarMeta([FromBody] MetaLojaRequest req, CancellationToken ct)
    {
        var m = await db.MetasPremiacaoLoja.FirstOrDefaultAsync(x =>
            x.EmpresaId == req.EmpresaId && x.LocalEstoqueId == req.LocalEstoqueId && x.Ano == req.Ano && x.Mes == req.Mes, ct);
        if (m is null)
        {
            m = MetaPremiacaoLoja.Criar(req.EmpresaId, req.LocalEstoqueId, req.Ano, req.Mes, req.MetaLoja, req.MetaIndividual);
            db.MetasPremiacaoLoja.Add(m);
        }
        else m.Atualizar(req.MetaLoja, req.MetaIndividual);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Remove o override manual → volta a meta automática (base financeira).</summary>
    [HttpDelete("metas")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> RemoverMeta([FromQuery] Guid empresaId, [FromQuery] Guid localEstoqueId,
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        var m = await db.MetasPremiacaoLoja.FirstOrDefaultAsync(x =>
            x.EmpresaId == empresaId && x.LocalEstoqueId == localEstoqueId && x.Ano == ano && x.Mes == mes, ct);
        if (m is not null) { db.MetasPremiacaoLoja.Remove(m); await db.SaveChangesAsync(ct); }
        return NoContent();
    }

    /// <summary>Resolve a meta de cada loja no mês: override manual se houver, senão automática
    /// (faturamento médio dos últimos MesesBaseMeta meses × FatorMetaLoja; individual = ÷ nº vendedores).</summary>
    private async Task<Dictionary<Guid, (decimal MetaLoja, decimal MetaIndividual, decimal BaseFaturamento, int Vendedores, bool Manual)>>
        ResolverMetasAsync(Guid empresaId, int ano, int mes, ConfiguracaoPremiacao cfg, CancellationToken ct)
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
            .Select(v => new { v.LocalEstoqueId, v.VendedorId })
            .Distinct().ToListAsync(ct))
            .GroupBy(x => x.LocalEstoqueId).ToDictionary(g => g.Key, g => g.Count());

        var overrides = await db.MetasPremiacaoLoja.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Ano == ano && x.Mes == mes)
            .ToDictionaryAsync(x => x.LocalEstoqueId, x => x, ct);

        var lojas = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId).Select(l => l.Id).ToListAsync(ct);

        var dict = new Dictionary<Guid, (decimal, decimal, decimal, int, bool)>();
        foreach (var loja in lojas)
        {
            var fat = fatBase.TryGetValue(loja, out var f) ? f : 0m;
            var media = Math.Round(fat / cfg.MesesBaseMeta, 2);
            var vend = vendBase.TryGetValue(loja, out var vv) ? vv : 0;
            if (overrides.TryGetValue(loja, out var ov))
                dict[loja] = (ov.MetaLoja, ov.MetaIndividual, media, vend, true);
            else
            {
                var metaLoja = Math.Round(media * cfg.FatorMetaLoja / 100m, 2);
                var metaInd = Math.Round(metaLoja / Math.Max(1, vend), 2);
                dict[loja] = (metaLoja, metaInd, media, vend, false);
            }
        }
        return dict;
    }

    // ── Avaliação semanal (Performance Comercial) ─────────────────────────
    [HttpGet("avaliacoes")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> ListarAvaliacoes([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, [FromQuery] Guid? colaboradorId, CancellationToken ct)
    {
        var q = db.AvaliacoesDesempenho.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.Ano == ano && a.Mes == mes);
        if (colaboradorId.HasValue) q = q.Where(a => a.ColaboradorId == colaboradorId.Value);
        var lista = await q.ToListAsync(ct);
        return Ok(lista.Select(a => new
        {
            a.Id, a.ColaboradorId, a.LocalEstoqueId, inicioSemana = a.InicioSemana.ToString("yyyy-MM-dd"),
            a.Abordagem, a.Diagnostico, a.ConexaoProduto, a.SugestaoComplementar, a.Fechamento,
            a.Abastecimento, a.Organizacao, a.Rotina, a.Validade, a.Perdas, a.Armazenamento,
            a.Observacao, pontos = a.Pontos
        }));
    }

    [HttpPost("avaliacoes")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> SalvarAvaliacao([FromBody] AvaliacaoRequest req, CancellationToken ct)
    {
        var inicio = DateTime.Parse(req.InicioSemana).Date;
        var a = await db.AvaliacoesDesempenho
            .FirstOrDefaultAsync(x => x.ColaboradorId == req.ColaboradorId && x.InicioSemana == inicio, ct);
        if (a is null)
        {
            a = AvaliacaoDesempenhoSemanal.Criar(req.EmpresaId, req.LocalEstoqueId, req.ColaboradorId, inicio);
            db.AvaliacoesDesempenho.Add(a);
        }
        NivelItem N(int v) => v >= 100 ? NivelItem.Consistente : v >= 50 ? NivelItem.Parcial : NivelItem.NaoRealizado;
        a.Definir(N(req.Abordagem), N(req.Diagnostico), N(req.ConexaoProduto), N(req.SugestaoComplementar), N(req.Fechamento),
            N(req.Abastecimento), N(req.Organizacao), N(req.Rotina), N(req.Validade), N(req.Perdas), N(req.Armazenamento), req.Observacao);
        await db.SaveChangesAsync(ct);
        return Ok(new { a.Id, pontos = a.Pontos });
    }

    // ── Apuração mensal (elegibilidade + cortes) ──────────────────────────
    [HttpPost("apuracao-mensal")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> SalvarApuracao([FromBody] ApuracaoRequest req, CancellationToken ct)
    {
        var a = await db.ApuracoesPremiacao
            .FirstOrDefaultAsync(x => x.ColaboradorId == req.ColaboradorId && x.Ano == req.Ano && x.Mes == req.Mes, ct);
        if (a is null)
        {
            a = ApuracaoMensalPremiacao.Criar(req.EmpresaId, req.LocalEstoqueId, req.ColaboradorId, req.Ano, req.Mes);
            db.ApuracoesPremiacao.Add(a);
        }
        a.Definir(req.PresencaPercent, req.FaltaInjustificada, req.Advertencia, req.ExecucaoMinima,
            req.ProdutoVencidoExposto, req.HigieneGrave, req.RotinaNaoExecutada, req.ReclamacaoRelevante, req.Observacao);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Apuração / resultado (gestor vê todos) ────────────────────────────
    [HttpGet("apuracao")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> Apuracao([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        var resultados = await CalcularAsync(empresaId, ano, mes, null, ct);
        var porLoja = resultados
            .GroupBy(r => new { r.LocalEstoqueId, r.MetaLoja, r.FaturamentoLoja, r.PercentLoja })
            .Select(g => new
            {
                lojaId = g.Key.LocalEstoqueId,
                loja = g.First().LojaNome,
                faturamentoLoja = g.Key.FaturamentoLoja,
                metaLoja = g.Key.MetaLoja,
                percentLoja = g.Key.PercentLoja,
                totalPremios = Math.Round(g.Sum(x => x.Res.Premio), 2),
                colaboradores = g.OrderByDescending(x => x.Res.Premio).Select(x => Dto(x.Res)).ToList()
            }).OrderByDescending(x => x.faturamentoLoja).ToList();
        return Ok(new { ano, mes, lojas = porLoja });
    }

    // ── Meu desempenho (o próprio colaborador) ────────────────────────────
    [HttpGet("meu-desempenho")]
    public async Task<IActionResult> MeuDesempenho([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        var resultados = await CalcularAsync(empresaId, ano, mes, UsuarioId, ct);
        if (resultados.Count == 0) return Ok(new { semDados = true });
        var meu = resultados[0];
        return Ok(Dto(meu.Res, incluirSemanas: true, avaliacoes: meu.Avaliacoes));
    }

    // ── Termo de Aceite (assinatura digital) ─────────────────────────────
    [HttpGet("meu-aceite")]
    public async Task<IActionResult> MeuAceite([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var a = await db.AceitesTermoPremiacao.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.ColaboradorId == UsuarioId)
            .OrderByDescending(x => x.DataAceite).FirstOrDefaultAsync(ct);
        return Ok(new { aceito = a != null, dataAceite = a?.DataAceite, termoVersao = a?.TermoVersao });
    }

    [HttpPost("aceitar")]
    public async Task<IActionResult> Aceitar([FromBody] AceiteRequest req, CancellationToken ct)
    {
        if (UsuarioId == Guid.Empty) return Unauthorized();
        var nome = User.FindFirst("nome")?.Value ?? "Colaborador";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var a = AceiteTermoPremiacao.Criar(req.EmpresaId, UsuarioId, nome,
            string.IsNullOrWhiteSpace(req.TermoVersao) ? "1.0" : req.TermoVersao!, req.TermoHash ?? "",
            req.FotoBase64, req.AssinaturaBase64, req.Latitude, req.Longitude, req.PrecisaoMetros,
            ip, ua?.Length > 400 ? ua[..400] : ua);
        db.AceitesTermoPremiacao.Add(a);
        await db.SaveChangesAsync(ct);
        return Ok(new { a.Id, a.DataAceite });
    }

    /// <summary>Gestor: lista/consulta os aceites (trilha de auditoria).</summary>
    [HttpGet("aceites")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> Aceites([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lista = await db.AceitesTermoPremiacao.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId)
            .OrderByDescending(x => x.DataAceite)
            .Select(x => new
            {
                x.Id, x.ColaboradorNome, x.DataAceite, x.TermoVersao, x.TermoHash,
                x.Latitude, x.Longitude, x.PrecisaoMetros, x.Ip,
                x.FotoBase64, x.AssinaturaBase64
            }).ToListAsync(ct);
        return Ok(lista);
    }

    /// <summary>Gestor: comprovante em PDF de um aceite (com foto, assinatura, geo e trilha).</summary>
    [HttpGet("aceites/{id:guid}/comprovante-pdf")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> ComprovantePdf(Guid id, CancellationToken ct)
    {
        var a = await db.AceitesTermoPremiacao.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == a.EmpresaId, ct);

        var foto = DecodeDataUrl(a.FotoBase64);
        var assinatura = DecodeDataUrl(a.AssinaturaBase64);
        var dataLocal = a.DataAceite.ToLocalTime();

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Margin(36); page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor("#1b241e"));

                page.Header().Column(h =>
                {
                    h.Item().Text("Comprovante de Aceite Eletrônico").FontSize(16).Bold().FontColor("#1e7a46");
                    h.Item().Text("Regulamento de Premiação por Desempenho").FontSize(11).FontColor("#555");
                    h.Item().PaddingTop(2).Text(empresa?.RazaoSocial ?? "").FontSize(9).FontColor("#777");
                });

                page.Content().PaddingVertical(14).Column(c =>
                {
                    c.Spacing(8);
                    void Linha(string k, string v) => c.Item().Row(r =>
                    {
                        r.ConstantItem(150).Text(k).SemiBold().FontColor("#555");
                        r.RelativeItem().Text(v);
                    });

                    Linha("Colaborador(a):", a.ColaboradorNome);
                    Linha("Data e hora do aceite:", dataLocal.ToString("dd/MM/yyyy HH:mm:ss") + " (horário local)");
                    Linha("Versão do termo:", a.TermoVersao);
                    Linha("Hash do documento (SHA-256):", string.IsNullOrEmpty(a.TermoHash) ? "—" : a.TermoHash);
                    Linha("Localização:", a.Latitude.HasValue
                        ? $"{a.Latitude:0.#####}, {a.Longitude:0.#####}  (±{a.PrecisaoMetros:0}m)" : "não informada");
                    Linha("Endereço do mapa:", a.Latitude.HasValue
                        ? $"https://maps.google.com/?q={a.Latitude},{a.Longitude}" : "—");
                    Linha("IP:", a.Ip ?? "—");
                    Linha("Dispositivo (User-Agent):", a.UserAgent ?? "—");

                    c.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Foto (identificação)").SemiBold().FontColor("#555").FontSize(9);
                            if (foto is not null) col.Item().PaddingTop(4).Height(160).Image(foto).FitArea();
                            else col.Item().PaddingTop(4).Text("—");
                        });
                        r.ConstantItem(16);
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Assinatura").SemiBold().FontColor("#555").FontSize(9);
                            if (assinatura is not null) col.Item().PaddingTop(4).Border(0.5f).Height(160).Image(assinatura).FitArea();
                            else col.Item().PaddingTop(4).Text("—");
                        });
                    });

                    c.Item().PaddingTop(10).Background("#F3F5F0").Padding(10).Text(
                        "Declaração: a colaboradora acima, mediante autenticação por usuário e senha no sistema, "
                        + "leu e aceitou o Regulamento de Premiação por Desempenho, confirmando sua participação e "
                        + "ciência de que o prêmio não possui natureza salarial. As evidências (foto, assinatura, "
                        + "geolocalização, IP, data/hora e hash do documento) foram registradas no ato do aceite para fins de comprovação.")
                        .FontSize(9).FontColor("#444");
                });

                page.Footer().AlignCenter().Text($"Documento gerado em {DateTime.Now:dd/MM/yyyy HH:mm} · id {a.Id}")
                    .FontSize(8).FontColor("#999");
            });
        });

        return File(pdf.GeneratePdf(), "application/pdf", $"aceite-{a.ColaboradorNome}.pdf");
    }

    private static byte[]? DecodeDataUrl(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl)) return null;
        var i = dataUrl.IndexOf(',');
        var b64 = i >= 0 ? dataUrl[(i + 1)..] : dataUrl;
        try { return Convert.FromBase64String(b64); } catch { return null; }
    }

    // ── Núcleo do cálculo ─────────────────────────────────────────────────
    private async Task<List<(ResultadoPremio Res, string LojaNome, Guid LocalEstoqueId,
        decimal FaturamentoLoja, decimal MetaLoja, decimal PercentLoja, List<AvaliacaoDesempenhoSemanal> Avaliacoes)>>
        CalcularAsync(Guid empresaId, int ano, int mes, Guid? apenasColaborador, CancellationToken ct)
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

        // Lojas com produto vencido em estoque → desconta os pontos de "Validade" (10)
        // da performance de TODOS os colaboradores daquela unidade (automático).
        var hoje = DateTime.Today;
        var lojasComVencido = new HashSet<Guid>(await db.Lotes.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Quantidade > 0
                && l.DataValidade != null && l.DataValidade < hoje)
            .Select(l => l.LocalEstoqueId).Distinct().ToListAsync(ct));

        // Roster = quem teve atividade no período (venda, avaliação ou apuração), com loja
        // no cadastro. Não depende do flag Ativo (vendedores podem estar sem login/inativos).
        var idsAtividade = new HashSet<Guid>(vendaVendedor.Keys);
        idsAtividade.UnionWith(avaliacoes.Keys);
        idsAtividade.UnionWith(apuracoes.Keys);
        if (apenasColaborador.HasValue)
            idsAtividade = new HashSet<Guid> { apenasColaborador.Value };

        var roster = await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.LocalEstoqueId != null && idsAtividade.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.LocalEstoqueId })
            .ToListAsync(ct);

        var lista = new List<(ResultadoPremio, string, Guid, decimal, decimal, decimal, List<AvaliacaoDesempenhoSemanal>)>();
        foreach (var u in roster)
        {
            var loja = u.LocalEstoqueId!.Value;
            var metaTup = metas.TryGetValue(loja, out var mt) ? mt : default;
            var metaLoja = metaTup.MetaLoja;
            var metaInd = metaTup.MetaIndividual;
            // Valor base por colaborador: fixo, ou dinâmico = % da meta individual (proporcional
            // ao faturamento esperado → nunca fere o faturamento da loja).
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
                    // Zera o item Validade (10 pts × nível) em cada semana.
                    perf = Math.Round(avalsU.Average(a => Math.Max(0, a.Pontos - 10m * (int)a.Validade / 100m)), 1);
                    descValidade = Math.Round(avalsU.Average(a => 10m * (int)a.Validade / 100m), 1);
                }
                else perf = Math.Round(avalsU.Average(a => a.Pontos), 1);
            }
            var apu = apuracoes.TryGetValue(u.Id, out var ap) ? ap : null;

            var res = CalculoPremiacao.Calcular(u.Id, u.Nome, loja, fat, metaLoja, vendaInd, metaInd,
                perf, avalsU.Count, cfg, valorBaseLoja, apu, descValidade);
            var pctLoja = metaLoja > 0 ? Math.Round(fat / metaLoja * 100, 1) : 0;
            lista.Add((res, lojas.TryGetValue(loja, out var ln) ? ln : "—", loja, fat, metaLoja, pctLoja, avalsU));
        }
        return lista;
    }

    private static object Dto(ResultadoPremio r, bool incluirSemanas = false, List<AvaliacaoDesempenhoSemanal>? avaliacoes = null)
        => new
        {
            r.ColaboradorId, colaborador = r.Colaborador,
            faturamentoLoja = r.FaturamentoLoja, metaLoja = r.MetaLoja, percentLoja = r.PercentLoja,
            vendaIndividual = r.VendaIndividual, metaIndividual = r.MetaIndividual, percentIndividual = r.PercentIndividual,
            performancePercent = r.PerformancePercent, semanasAvaliadas = r.SemanasAvaliadas,
            baseLoja = r.BaseLoja, fatorIndividual = r.FatorIndividual,
            elegivel = r.Elegivel, temCorte = r.TemCorte, motivo = r.Motivo, premio = r.Premio,
            descontoValidade = r.DescontoValidade,
            avaliacoes = incluirSemanas && avaliacoes != null
                ? avaliacoes.OrderBy(a => a.InicioSemana).Select(a => new { inicioSemana = a.InicioSemana.ToString("yyyy-MM-dd"), pontos = a.Pontos }).ToList<object>()
                : null
        };
}

public record ConfigPremiacaoRequest(Guid EmpresaId, decimal ValorBase, decimal RedutorPercent,
    decimal MinPresenca, decimal ThresholdLoja, decimal ThresholdIndividual,
    decimal FatorMetaLoja, int MesesBaseMeta, bool ValorBaseDinamico, decimal PercentFaturamentoPremio, bool Ativo);
public record MetaLojaRequest(Guid EmpresaId, Guid LocalEstoqueId, int Ano, int Mes, decimal MetaLoja, decimal MetaIndividual);
public record AvaliacaoRequest(Guid EmpresaId, Guid LocalEstoqueId, Guid ColaboradorId, string InicioSemana,
    int Abordagem, int Diagnostico, int ConexaoProduto, int SugestaoComplementar, int Fechamento,
    int Abastecimento, int Organizacao, int Rotina, int Validade, int Perdas, int Armazenamento, string? Observacao);
public record ApuracaoRequest(Guid EmpresaId, Guid LocalEstoqueId, Guid ColaboradorId, int Ano, int Mes,
    decimal PresencaPercent, bool FaltaInjustificada, bool Advertencia, bool ExecucaoMinima,
    bool ProdutoVencidoExposto, bool HigieneGrave, bool RotinaNaoExecutada, bool ReclamacaoRelevante, string? Observacao);
public record AceiteRequest(Guid EmpresaId, string? TermoVersao, string? TermoHash,
    string? FotoBase64, string? AssinaturaBase64, double? Latitude, double? Longitude, double? PrecisaoMetros);
