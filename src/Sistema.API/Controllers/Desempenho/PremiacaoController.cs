using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Security.Claims;
using Sistema.Domain.Desempenho.Entities;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Desempenho;
using Sistema.Infrastructure.Jobs;

namespace Sistema.API.Controllers.Desempenho;

/// <summary>Premiação por Desempenho: metas, avaliação semanal, apuração e cálculo do prêmio.</summary>
[ApiController]
[Route("api/premiacao")]
[Authorize]
public class PremiacaoController(SistemaDbContext db, PremiacaoCalculoService calc, ArquivarDemonstrativosJob arquivador) : ControllerBase
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
        var metas = await calc.ResolverMetasAsync(empresaId, a, m, cfg, ct);
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
        var resultados = await calc.CalcularAsync(empresaId, ano, mes, null, ct);
        var porLoja = resultados
            .GroupBy(r => new { r.LocalEstoqueId, r.MetaLoja, r.FaturamentoLoja, r.PercentLoja })
            .Select(g => new
            {
                lojaId = g.Key.LocalEstoqueId,
                loja = g.First().LojaNome,
                faturamentoLoja = g.Key.FaturamentoLoja,
                metaLoja = g.Key.MetaLoja,
                percentLoja = g.Key.PercentLoja,
                projecaoLoja = ProjecaoLinha(g.Key.FaturamentoLoja, g.Key.MetaLoja, ano, mes),
                totalPremios = Math.Round(g.Sum(x => x.Res.Premio), 2),
                colaboradores = g.OrderByDescending(x => x.Res.Premio)
                    .Select(x => Dto(x.Res, projecao: ProjecaoLinha(x.Res.VendaIndividual, x.Res.MetaIndividual, ano, mes))).ToList()
            }).OrderByDescending(x => x.faturamentoLoja).ToList();
        return Ok(new { ano, mes, lojas = porLoja });
    }

    // ── Meu desempenho (o próprio colaborador) ────────────────────────────
    [HttpGet("meu-desempenho")]
    public async Task<IActionResult> MeuDesempenho([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, CancellationToken ct)
    {
        var resultados = await calc.CalcularAsync(empresaId, ano, mes, UsuarioId, ct);
        if (resultados.Count == 0) return Ok(new { semDados = true });
        var meu = resultados[0];

        // Projeção pelo ritmo (só no mês corrente): no ritmo atual, vai/não vai bater a meta
        object? projecao = null;
        var hoje = DateTime.Today;
        if (ano == hoje.Year && mes == hoje.Month)
        {
            var r = meu.Res;
            var diasNoMes = DateTime.DaysInMonth(ano, mes);
            var diasDec = hoje.Day;
            var diasRest = Math.Max(0, diasNoMes - diasDec);
            decimal Proj(decimal feito) => diasDec > 0 ? Math.Round(feito / diasDec * diasNoMes, 2) : 0m;
            object Linha(decimal feito, decimal meta)
            {
                var proj = Proj(feito);
                var falta = Math.Max(0m, meta - feito);
                return new
                {
                    realizado = Math.Round(feito, 2), meta = Math.Round(meta, 2), projecao = proj,
                    vaiBater = meta > 0 && proj >= meta,
                    falta, porDia = diasRest > 0 ? Math.Round(falta / diasRest, 2) : falta,
                    percentProjecao = meta > 0 ? Math.Round(proj / meta * 100, 0) : (decimal?)null,
                    percentAtual = meta > 0 ? Math.Round(feito / meta * 100, 0) : (decimal?)null,
                };
            }
            projecao = new
            {
                diasNoMes, diasDecorridos = diasDec, diasRestantes = diasRest,
                individual = Linha(r.VendaIndividual, r.MetaIndividual),
                loja = Linha(r.FaturamentoLoja, r.MetaLoja),
            };
        }

        return Ok(Dto(meu.Res, incluirSemanas: true, avaliacoes: meu.Avaliacoes, projecao: projecao));
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

    /// <summary>Texto do Regulamento (com as metas da loja do colaborador) para leitura/assinatura.</summary>
    [HttpGet("regulamento")]
    public async Task<IActionResult> Regulamento([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, [FromQuery] Guid? localEstoqueId, CancellationToken ct)
    {
        var a = ano ?? DateTime.Today.Year;
        var m = mes ?? DateTime.Today.Month;
        var loja = localEstoqueId
            ?? (Guid.TryParse(User.FindFirst("localEstoqueId")?.Value, out var lid) ? lid : Guid.Empty);

        var cfg = await db.ConfiguracoesPremiacao.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct) ?? ConfiguracaoPremiacao.Padrao(empresaId);
        var metas = await calc.ResolverMetasAsync(empresaId, a, m, cfg, ct);
        var mt = loja != Guid.Empty && metas.TryGetValue(loja, out var v) ? v
            : (metas.Count > 0 ? metas.Values.First() : new MetaResolvida(0, 0, 0, 0, false));
        if (loja == Guid.Empty) loja = metas.FirstOrDefault(kv => kv.Value.Equals(mt)).Key;

        var lojaNome = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.Id == loja).Select(l => l.Nome).FirstOrDefaultAsync(ct) ?? "—";
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId, ct);

        var valorBase = cfg.ValorBaseDinamico && mt.MetaIndividual > 0
            ? Math.Round(mt.MetaIndividual * cfg.PercentFaturamentoPremio / 100m, 2)
            : cfg.ValorBase;
        var valorBaseReduzido = Math.Round(valorBase * cfg.RedutorPercent / 100m, 2);

        var texto = RegulamentoPremiacao.Gerar(empresa?.RazaoSocial ?? "", lojaNome, a, m,
            mt.MetaLoja, mt.MetaIndividual, valorBase, valorBaseReduzido,
            cfg.MinPresenca, cfg.ThresholdLoja, cfg.ThresholdIndividual, cfg.RedutorPercent);

        return Ok(new
        {
            versao = RegulamentoPremiacao.Versao,
            loja = lojaNome, ano = a, mes = m,
            metaLoja = mt.MetaLoja, metaIndividual = mt.MetaIndividual,
            valorBase, valorBaseReduzido,
            texto
        });
    }

    [HttpPost("aceitar")]
    public async Task<IActionResult> Aceitar([FromBody] AceiteRequest req, CancellationToken ct)
    {
        if (UsuarioId == Guid.Empty) return Unauthorized();
        var nome = User.FindFirst("nome")?.Value ?? "Colaborador";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var a = AceiteTermoPremiacao.Criar(req.EmpresaId, UsuarioId, nome,
            string.IsNullOrWhiteSpace(req.TermoVersao) ? RegulamentoPremiacao.Versao : req.TermoVersao!, req.TermoHash ?? "",
            req.FotoBase64, req.AssinaturaBase64, req.Latitude, req.Longitude, req.PrecisaoMetros,
            ip, ua?.Length > 400 ? ua[..400] : ua, req.TextoRegulamento);
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

                    // Regulamento exato que foi aceito (com os valores das metas), em páginas seguintes.
                    if (!string.IsNullOrWhiteSpace(a.TextoRegulamento))
                    {
                        c.Item().PageBreak();
                        c.Item().Text("Regulamento aceito (íntegra)").FontSize(12).Bold().FontColor("#1e7a46");
                        foreach (var raw in a.TextoRegulamento.Split('\n'))
                        {
                            var linha = raw.TrimEnd();
                            if (linha.Length == 0) { c.Item().Height(4); continue; }
                            var destaque = linha.StartsWith("CLÁUSULA") || linha.StartsWith("TERMO DE")
                                || linha.StartsWith("Colaborador:");
                            var it = c.Item().Text(linha).FontSize(8.5f).FontColor("#333");
                            if (destaque) it.SemiBold();
                        }
                    }
                });

                var urlVerif = $"{BaseUrl()}/api/premiacao/verificar/aceite/{a.Id}";
                var qr = PremiacaoCalculoService.GerarQrPng(urlVerif);
                page.Footer().PaddingTop(8).Row(f =>
                {
                    f.ConstantItem(60).Image(qr).FitArea();
                    f.RelativeItem().PaddingLeft(8).AlignMiddle().Column(col =>
                    {
                        col.Item().Text("Verificação de autenticidade").FontSize(8).SemiBold().FontColor("#555");
                        col.Item().Text(urlVerif).FontSize(7).FontColor("#1e7a46");
                        col.Item().Text($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm} · id {a.Id}").FontSize(7).FontColor("#999");
                    });
                });
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

    private string BaseUrl() => $"{Request.Scheme}://{Request.Host}";

    // ── Arquivo mensal dos demonstrativos ─────────────────────────────────
    /// <summary>Gera/arquiva manualmente os demonstrativos de uma competência (gestor).</summary>
    [HttpPost("arquivar")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> Arquivar([FromQuery] Guid empresaId, [FromQuery] int ano, [FromQuery] int mes)
    {
        var qtd = await arquivador.ArquivarAsync(empresaId, ano, mes);
        return Ok(new { arquivados = qtd, competencia = $"{mes:00}/{ano}" });
    }

    [HttpGet("arquivo")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> ListarArquivo([FromQuery] Guid empresaId,
        [FromQuery] int? ano, [FromQuery] int? mes, CancellationToken ct)
    {
        var q = db.DemonstrativosArquivados.AsNoTracking().Where(x => x.EmpresaId == empresaId);
        if (ano.HasValue) q = q.Where(x => x.Ano == ano.Value);
        if (mes.HasValue) q = q.Where(x => x.Mes == mes.Value);
        var lista = await q.OrderByDescending(x => x.Ano).ThenByDescending(x => x.Mes).ThenBy(x => x.ColaboradorNome)
            .Select(x => new { x.Id, x.ColaboradorNome, x.Ano, x.Mes, competencia = $"{x.Mes:00}/{x.Ano}", x.Premio, x.GeradoEm })
            .ToListAsync(ct);
        return Ok(lista);
    }

    [HttpGet("arquivo/{id:guid}/pdf")]
    public async Task<IActionResult> ArquivoPdf(Guid id, CancellationToken ct)
    {
        var a = await db.DemonstrativosArquivados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();
        var ehGestor = User.IsInRole("Administrador") || User.IsInRole("Financeiro");
        if (!ehGestor && a.ColaboradorId != UsuarioId) return Forbid();
        return File(a.Pdf, "application/pdf", $"premio-{a.ColaboradorNome}-{a.Ano}-{a.Mes:00}.pdf");
    }

    // ── Verificação pública (QR) ──────────────────────────────────────────
    [HttpGet("verificar/aceite/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> VerificarAceite(Guid id, CancellationToken ct)
    {
        var a = await db.AceitesTermoPremiacao.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Ok(new { autentico = false });
        return Ok(new
        {
            autentico = true, tipo = "Aceite do Regulamento de Premiação",
            colaborador = a.ColaboradorNome, data = a.DataAceite.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            versao = a.TermoVersao, hash = a.TermoHash
        });
    }

    [HttpGet("verificar/premio")]
    [AllowAnonymous]
    public async Task<IActionResult> VerificarPremio([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, [FromQuery] Guid colaboradorId, CancellationToken ct)
    {
        var res = await calc.CalcularAsync(empresaId, ano, mes, colaboradorId, ct);
        if (res.Count == 0) return Ok(new { encontrado = false });
        var r = res[0].Res;
        return Ok(new
        {
            encontrado = true, tipo = "Demonstrativo de Premiação",
            colaborador = r.Colaborador, competencia = $"{mes:00}/{ano}",
            metaIndividual = r.MetaIndividual, vendaIndividual = r.VendaIndividual,
            performance = r.PerformancePercent, premio = r.Premio
        });
    }

    // ── Demonstrativo mensal do prêmio (PDF, por colaborador) ─────────────
    [HttpGet("demonstrativo-pdf")]
    public async Task<IActionResult> DemonstrativoPdf([FromQuery] Guid empresaId,
        [FromQuery] int ano, [FromQuery] int mes, [FromQuery] Guid colaboradorId, CancellationToken ct)
    {
        // Gestor vê de qualquer um; colaborador só o próprio.
        var ehGestor = User.IsInRole("Administrador") || User.IsInRole("Financeiro");
        if (!ehGestor && colaboradorId != UsuarioId) return Forbid();

        var lista = await calc.CalcularAsync(empresaId, ano, mes, colaboradorId, ct);
        if (lista.Count == 0) return NotFound();
        var t = lista[0];
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        var urlVerif = $"{BaseUrl()}/api/premiacao/verificar/premio?empresaId={empresaId}&ano={ano}&mes={mes}&colaboradorId={colaboradorId}";
        var bytes = calc.GerarDemonstrativoPdf(t, empresa?.RazaoSocial ?? "", ano, mes, urlVerif);
        return File(bytes, "application/pdf", $"premio-{t.Res.Colaborador}-{ano}-{mes:00}.pdf");
    }

    /// <summary>Projeção pelo ritmo do mês corrente (null nos meses fechados).</summary>
    private static object? ProjecaoLinha(decimal feito, decimal meta, int ano, int mes)
    {
        var hoje = DateTime.Today;
        if (ano != hoje.Year || mes != hoje.Month) return null;
        var diasNoMes = DateTime.DaysInMonth(ano, mes);
        var diasDec = hoje.Day;
        var diasRest = Math.Max(0, diasNoMes - diasDec);
        var proj = diasDec > 0 ? Math.Round(feito / diasDec * diasNoMes, 2) : 0m;
        var falta = Math.Max(0m, meta - feito);
        return new
        {
            diasNoMes, diasDecorridos = diasDec, diasRestantes = diasRest,
            realizado = Math.Round(feito, 2), meta = Math.Round(meta, 2), projecao = proj,
            vaiBater = meta > 0 && proj >= meta, falta,
            porDia = diasRest > 0 ? Math.Round(falta / diasRest, 2) : falta,
            percentProjecao = meta > 0 ? Math.Round(proj / meta * 100, 0) : (decimal?)null,
            percentAtual = meta > 0 ? Math.Round(feito / meta * 100, 0) : (decimal?)null,
        };
    }

    private static object Dto(ResultadoPremio r, bool incluirSemanas = false, List<AvaliacaoDesempenhoSemanal>? avaliacoes = null,
        object? projecao = null)
        => new
        {
            r.ColaboradorId, colaborador = r.Colaborador,
            faturamentoLoja = r.FaturamentoLoja, metaLoja = r.MetaLoja, percentLoja = r.PercentLoja,
            vendaIndividual = r.VendaIndividual, metaIndividual = r.MetaIndividual, percentIndividual = r.PercentIndividual,
            performancePercent = r.PerformancePercent, semanasAvaliadas = r.SemanasAvaliadas,
            baseLoja = r.BaseLoja, fatorIndividual = r.FatorIndividual,
            elegivel = r.Elegivel, temCorte = r.TemCorte, motivo = r.Motivo, premio = r.Premio,
            descontoValidade = r.DescontoValidade,
            descontoAtendimentoWhatsapp = r.DescontoAtendimentoWhatsApp,
            projecao,
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
    string? FotoBase64, string? AssinaturaBase64, double? Latitude, double? Longitude, double? PrecisaoMetros,
    string? TextoRegulamento = null);
