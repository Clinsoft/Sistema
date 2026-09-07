using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public async Task<IActionResult> ObterConfig([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesPremiacao.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct) ?? ConfiguracaoPremiacao.Padrao(empresaId);
        var metas = await (from m in db.MetasPremiacaoLoja.AsNoTracking()
                           join l in db.LocaisEstoque.AsNoTracking() on m.LocalEstoqueId equals l.Id
                           where m.EmpresaId == empresaId
                           select new { m.LocalEstoqueId, loja = l.Nome, m.MetaLoja, m.MetaIndividual }).ToListAsync(ct);
        return Ok(new
        {
            cfg.ValorBase, cfg.RedutorPercent, cfg.MinPresenca, cfg.ThresholdLoja, cfg.ThresholdIndividual, cfg.Ativo,
            metas
        });
    }

    [HttpPut("config")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> SalvarConfig([FromBody] ConfigPremiacaoRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesPremiacao.FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);
        if (cfg is null) { cfg = ConfiguracaoPremiacao.Padrao(req.EmpresaId); db.ConfiguracoesPremiacao.Add(cfg); }
        cfg.Atualizar(req.ValorBase, req.RedutorPercent, req.MinPresenca, req.ThresholdLoja, req.ThresholdIndividual, req.Ativo);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("metas")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> SalvarMeta([FromBody] MetaLojaRequest req, CancellationToken ct)
    {
        var m = await db.MetasPremiacaoLoja
            .FirstOrDefaultAsync(x => x.EmpresaId == req.EmpresaId && x.LocalEstoqueId == req.LocalEstoqueId, ct);
        if (m is null)
        {
            m = MetaPremiacaoLoja.Criar(req.EmpresaId, req.LocalEstoqueId, req.MetaLoja, req.MetaIndividual);
            db.MetasPremiacaoLoja.Add(m);
        }
        else m.Atualizar(req.MetaLoja, req.MetaIndividual);
        await db.SaveChangesAsync(ct);
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

    // ── Núcleo do cálculo ─────────────────────────────────────────────────
    private async Task<List<(ResultadoPremio Res, string LojaNome, Guid LocalEstoqueId,
        decimal FaturamentoLoja, decimal MetaLoja, decimal PercentLoja, List<AvaliacaoDesempenhoSemanal> Avaliacoes)>>
        CalcularAsync(Guid empresaId, int ano, int mes, Guid? apenasColaborador, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesPremiacao.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct) ?? ConfiguracaoPremiacao.Padrao(empresaId);
        var inicio = new DateTime(ano, mes, 1);
        var fimExcl = inicio.AddMonths(1);

        var metas = await db.MetasPremiacaoLoja.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId)
            .ToDictionaryAsync(m => m.LocalEstoqueId, m => m, ct);

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
            var meta = metas.TryGetValue(loja, out var mt) ? mt : null;
            var metaLoja = meta?.MetaLoja ?? 0;
            var metaInd = meta?.MetaIndividual ?? 0;
            var fat = fatLoja.TryGetValue(loja, out var f) ? f : 0;
            var vendaInd = vendaVendedor.TryGetValue(u.Id, out var vi) ? vi : 0;
            var avalsU = avaliacoes.TryGetValue(u.Id, out var av) ? av : new List<AvaliacaoDesempenhoSemanal>();
            var perf = avalsU.Count > 0 ? Math.Round(avalsU.Average(a => a.Pontos), 1) : 0m;
            var apu = apuracoes.TryGetValue(u.Id, out var ap) ? ap : null;

            var res = CalculoPremiacao.Calcular(u.Id, u.Nome, loja, fat, metaLoja, vendaInd, metaInd,
                perf, avalsU.Count, cfg, apu);
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
            avaliacoes = incluirSemanas && avaliacoes != null
                ? avaliacoes.OrderBy(a => a.InicioSemana).Select(a => new { inicioSemana = a.InicioSemana.ToString("yyyy-MM-dd"), pontos = a.Pontos }).ToList<object>()
                : null
        };
}

public record ConfigPremiacaoRequest(Guid EmpresaId, decimal ValorBase, decimal RedutorPercent,
    decimal MinPresenca, decimal ThresholdLoja, decimal ThresholdIndividual, bool Ativo);
public record MetaLojaRequest(Guid EmpresaId, Guid LocalEstoqueId, decimal MetaLoja, decimal MetaIndividual);
public record AvaliacaoRequest(Guid EmpresaId, Guid LocalEstoqueId, Guid ColaboradorId, string InicioSemana,
    int Abordagem, int Diagnostico, int ConexaoProduto, int SugestaoComplementar, int Fechamento,
    int Abastecimento, int Organizacao, int Rotina, int Validade, int Perdas, int Armazenamento, string? Observacao);
public record ApuracaoRequest(Guid EmpresaId, Guid LocalEstoqueId, Guid ColaboradorId, int Ano, int Mes,
    decimal PresencaPercent, bool FaltaInjustificada, bool Advertencia, bool ExecucaoMinima,
    bool ProdutoVencidoExposto, bool HigieneGrave, bool RotinaNaoExecutada, bool ReclamacaoRelevante, string? Observacao);
