using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;

namespace Sistema.API.Controllers.Estoque;

/// <summary>
/// Análise de concorrência (Fase 1): geocodifica a loja e mapeia os concorrentes
/// num raio (padrão 5 km) via OpenStreetMap. Preços virão nas fases seguintes.
/// </summary>
[ApiController]
[Route("api/concorrentes")]
[Authorize(Roles = "Administrador,Gerente,Financeiro")]
public class ConcorrentesController(
    SistemaDbContext db, MapaConcorrenciaService mapa, GooglePlacesService google,
    MercadoLivreService ml) : ControllerBase
{
    /// <summary>Lojas da empresa com status de geocodificação e nº de concorrentes já mapeados.</summary>
    [HttpGet("lojas")]
    public async Task<IActionResult> Lojas([FromQuery] Guid empresaId, CancellationToken ct)
    {
        // Contagem de concorrentes ATIVOS por loja (query própria — evita quirk de
        // subconsulta correlacionada que ignorava o filtro Ativo).
        var contagem = (await db.Concorrentes.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .GroupBy(c => c.LocalEstoqueId)
            .Select(g => new { LocalEstoqueId = g.Key, Total = g.Count() })
            .ToListAsync(ct))
            .ToDictionary(x => x.LocalEstoqueId, x => x.Total);

        var locais = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId && l.Ativo)
            .OrderByDescending(l => l.Principal).ThenBy(l => l.Nome)
            .ToListAsync(ct);

        var lojas = locais.Select(l => new
        {
            l.Id, l.Nome, l.Latitude, l.Longitude,
            endereco = l.EnderecoFormatado(),
            geocodificada = l.Latitude != null && l.Longitude != null,
            concorrentes = contagem.TryGetValue(l.Id, out var n) ? n : 0
        });
        return Ok(lojas);
    }

    /// <summary>Concorrentes já mapeados de uma loja + a coordenada da loja.</summary>
    [HttpGet("loja/{localEstoqueId:guid}")]
    public async Task<IActionResult> DaLoja(Guid localEstoqueId, CancellationToken ct)
    {
        var loja = await db.LocaisEstoque.AsNoTracking()
            .Where(l => l.Id == localEstoqueId)
            .Select(l => new { l.Id, l.Nome, l.Latitude, l.Longitude, endereco = l.EnderecoFormatado() })
            .FirstOrDefaultAsync(ct);
        if (loja is null) return NotFound();

        var concorrentes = await db.Concorrentes.AsNoTracking()
            .Where(c => c.LocalEstoqueId == localEstoqueId && c.Ativo)
            .OrderBy(c => c.DistanciaKm)
            .Select(c => new
            {
                c.Id, c.Nome, c.Categoria, c.Latitude, c.Longitude, c.DistanciaKm,
                c.Endereco, c.Telefone, c.Website, c.Fonte
            })
            .ToListAsync(ct);

        return Ok(new { loja, concorrentes });
    }

    /// <summary>Geocodifica a loja pelo endereço cadastrado (OSM/Nominatim) e salva a coordenada.</summary>
    [HttpPost("loja/{localEstoqueId:guid}/geocodificar")]
    public async Task<IActionResult> Geocodificar(Guid localEstoqueId, CancellationToken ct)
    {
        var loja = await db.LocaisEstoque.FirstOrDefaultAsync(l => l.Id == localEstoqueId, ct);
        if (loja is null) return NotFound();

        if (!loja.TemEnderecoParaGeo())
            return BadRequest(new { mensagem = "A loja não tem endereço cadastrado. Preencha o endereço em Cadastros → Locais de Estoque." });

        var (lg, nu, ba, ci, uf, ce) = loja.EnderecoComponentes();
        var ponto = await mapa.GeocodificarAsync(lg, nu, ba, ci, uf, ce, ct);
        if (ponto is null)
            return BadRequest(new { mensagem = $"Não foi possível localizar no mapa: {loja.EnderecoFormatado()}. Revise o endereço da loja." });

        loja.DefinirCoordenadas(ponto.Lat, ponto.Lng);
        await db.SaveChangesAsync(ct);
        return Ok(new { loja.Latitude, loja.Longitude, endereco = loja.EnderecoFormatado() });
    }

    /// <summary>Varre o raio (km) e faz upsert dos concorrentes encontrados no OSM.</summary>
    [HttpPost("loja/{localEstoqueId:guid}/buscar")]
    public async Task<IActionResult> Buscar(Guid localEstoqueId, [FromQuery] double raioKm = 5, CancellationToken ct = default)
    {
        var loja = await db.LocaisEstoque.FirstOrDefaultAsync(l => l.Id == localEstoqueId, ct);
        if (loja is null) return NotFound();

        // Geocodifica na hora se ainda não tiver coordenada.
        if (loja.Latitude is null || loja.Longitude is null)
        {
            if (!loja.TemEnderecoParaGeo())
                return BadRequest(new { mensagem = "A loja não tem endereço cadastrado para localizar no mapa." });
            var (lg, nu, ba, ci, uf, ce) = loja.EnderecoComponentes();
            var p = await mapa.GeocodificarAsync(lg, nu, ba, ci, uf, ce, ct);
            if (p is null)
                return BadRequest(new { mensagem = "Não foi possível geocodificar o endereço da loja. Revise o endereço." });
            loja.DefinirCoordenadas(p.Lat, p.Lng);
            await db.SaveChangesAsync(ct);
        }

        var raioMetros = (int)Math.Clamp(raioKm, 0.5, 20) * 1000;

        // Fonte: Google Places (tem as lojas de naturais) quando configurado; senão OSM.
        var fonte = google.Configurado ? "Google" : "OSM";
        List<MapaConcorrenciaService.ConcorrenteOsm> achados;
        try
        {
            achados = fonte == "Google"
                ? await google.BuscarAsync(loja.Latitude!.Value, loja.Longitude!.Value, raioMetros, ct)
                : await mapa.BuscarConcorrentesAsync(loja.Latitude!.Value, loja.Longitude!.Value, raioMetros, ct);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { mensagem = $"Falha ao consultar a fonte ({fonte}): " + ex.Message });
        }

        // Remove a PRÓPRIA loja (a base às vezes tem a EcoGranel): pelo nome ou por estar
        // praticamente no mesmo ponto (<40 m).
        var nomeLoja = (loja.Nome ?? "").Trim();
        achados = achados.Where(a =>
            a.DistanciaKm > 0.04m
            && a.Nome.IndexOf("ecogranel", StringComparison.OrdinalIgnoreCase) < 0
            && (nomeLoja.Length < 4 || a.Nome.IndexOf(nomeLoja, StringComparison.OrdinalIgnoreCase) < 0))
            .ToList();

        // Upsert por (LocalEstoqueId, ref, fonte). Autoritativo para a MESMA fonte
        // (desativa os que sumiram); NÃO toca nos manuais nem na outra fonte.
        var existentes = await db.Concorrentes
            .Where(c => c.LocalEstoqueId == localEstoqueId && c.Fonte == fonte)
            .ToListAsync(ct);
        var porRef = existentes.Where(c => c.OsmRef != null).ToDictionary(c => c.OsmRef!, c => c);
        var achadosRefs = achados.Select(a => a.OsmRef).ToHashSet();

        int novos = 0, atualizados = 0, removidos = 0;
        foreach (var a in achados)
        {
            if (porRef.TryGetValue(a.OsmRef, out var ex))
            {
                ex.AtualizarDoOsm(a.Nome, a.Categoria, a.Lat, a.Lng, a.DistanciaKm, a.Endereco, a.Telefone, a.Website);
                if (!ex.Ativo) ex.Ativar();
                atualizados++;
            }
            else
            {
                db.Concorrentes.Add(Concorrente.CriarExterno(loja.EmpresaId, localEstoqueId, fonte, a.Nome,
                    a.Categoria, a.Lat, a.Lng, a.DistanciaKm, a.Endereco, a.Telefone, a.Website, a.OsmRef));
                novos++;
            }
        }
        foreach (var ex in existentes)
            if (ex.Ativo && ex.OsmRef != null && !achadosRefs.Contains(ex.OsmRef))
            {
                ex.Desativar();
                removidos++;
            }
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            loja = new { loja.Id, loja.Nome, loja.Latitude, loja.Longitude },
            raioKm, fonte, encontrados = achados.Count, novos, atualizados, removidos,
        });
    }

    public record ConcorrenteManualRequest(string Nome, string? Categoria, double Latitude, double Longitude, string? Endereco);

    /// <summary>Adiciona um concorrente MANUALMENTE (o OSM não cobre as lojas de naturais).</summary>
    [HttpPost("loja/{localEstoqueId:guid}/manual")]
    public async Task<IActionResult> AdicionarManual(Guid localEstoqueId,
        [FromBody] ConcorrenteManualRequest req, CancellationToken ct)
    {
        var loja = await db.LocaisEstoque.FirstOrDefaultAsync(l => l.Id == localEstoqueId, ct);
        if (loja is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Nome))
            return BadRequest(new { mensagem = "Informe o nome do concorrente." });
        if (req.Latitude is < -90 or > 90 || req.Longitude is < -180 or > 180)
            return BadRequest(new { mensagem = "Coordenada inválida." });

        decimal dist = 0m;
        if (loja.Latitude is not null && loja.Longitude is not null)
            dist = (decimal)Math.Round(
                MapaConcorrenciaService.DistanciaKm(loja.Latitude.Value, loja.Longitude.Value, req.Latitude, req.Longitude), 2);

        var categoria = string.IsNullOrWhiteSpace(req.Categoria) ? "Produtos naturais" : req.Categoria!.Trim();
        var c = Concorrente.CriarManual(loja.EmpresaId, localEstoqueId, req.Nome.Trim(),
            categoria, req.Latitude, req.Longitude, dist, req.Endereco);
        db.Concorrentes.Add(c);
        await db.SaveChangesAsync(ct);
        return Ok(new { c.Id, c.Nome, c.Categoria, c.Latitude, c.Longitude, c.DistanciaKm, c.Endereco, c.Telefone, c.Website, c.Fonte });
    }

    public record CoordRequest(double Latitude, double Longitude);

    /// <summary>Define manualmente a coordenada da loja (correção no mapa, arrastando o marcador).</summary>
    [HttpPost("loja/{localEstoqueId:guid}/coordenada")]
    public async Task<IActionResult> DefinirCoordenada(Guid localEstoqueId,
        [FromBody] CoordRequest req, CancellationToken ct)
    {
        var loja = await db.LocaisEstoque.FirstOrDefaultAsync(l => l.Id == localEstoqueId, ct);
        if (loja is null) return NotFound();
        if (req.Latitude is < -90 or > 90 || req.Longitude is < -180 or > 180)
            return BadRequest(new { mensagem = "Coordenada inválida." });
        loja.DefinirCoordenadas(req.Latitude, req.Longitude);
        await db.SaveChangesAsync(ct);
        return Ok(new { loja.Latitude, loja.Longitude });
    }

    /// <summary>Remove (desativa) um concorrente da lista.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id, CancellationToken ct)
    {
        var c = await db.Concorrentes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        c.Desativar();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Fase 2: preços (nosso × concorrente) ─────────────────────────────────

    /// <summary>Preços coletados de um concorrente (com o nosso preço quando ligado a produto).</summary>
    [HttpGet("concorrente/{concorrenteId:guid}/precos")]
    public async Task<IActionResult> PrecosDoConcorrente(Guid concorrenteId, CancellationToken ct)
    {
        var precos = await db.PrecosConcorrente.AsNoTracking()
            .Where(p => p.ConcorrenteId == concorrenteId)
            .OrderByDescending(p => p.DataColeta)
            .Select(p => new
            {
                p.Id, p.ProdutoId, p.Descricao, p.Ean, p.Preco, p.Unidade, p.DataColeta, p.Observacao,
                nossoPreco = p.ProdutoId != null
                    ? db.Produtos.Where(x => x.Id == p.ProdutoId).Select(x => (decimal?)x.PrecoVenda).FirstOrDefault()
                    : null,
                porPeso = p.ProdutoId != null
                    ? db.Produtos.Where(x => x.Id == p.ProdutoId).Select(x => (bool?)(x.ProdutoBalanca || x.VendidoFracionado)).FirstOrDefault()
                    : null,
            })
            .ToListAsync(ct);
        return Ok(precos);
    }

    public record PrecoRequest(Guid? ProdutoId, string Descricao, string? Ean,
        decimal Preco, string Unidade, string? Observacao);

    [HttpPost("concorrente/{concorrenteId:guid}/precos")]
    public async Task<IActionResult> AdicionarPreco(Guid concorrenteId,
        [FromBody] PrecoRequest req, CancellationToken ct)
    {
        var conc = await db.Concorrentes.FirstOrDefaultAsync(c => c.Id == concorrenteId, ct);
        if (conc is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Descricao))
            return BadRequest(new { mensagem = "Informe o produto/descrição." });
        if (req.Preco <= 0)
            return BadRequest(new { mensagem = "Informe um preço válido." });

        var p = PrecoConcorrente.Criar(conc.EmpresaId, concorrenteId, req.Descricao.Trim(),
            req.Preco, req.Unidade, req.ProdutoId, req.Ean, null, req.Observacao);
        db.PrecosConcorrente.Add(p);
        await db.SaveChangesAsync(ct);

        decimal? nosso = req.ProdutoId != null
            ? await db.Produtos.Where(x => x.Id == req.ProdutoId).Select(x => (decimal?)x.PrecoVenda).FirstOrDefaultAsync(ct)
            : null;
        return Ok(new { p.Id, p.ProdutoId, p.Descricao, p.Ean, p.Preco, p.Unidade, p.DataColeta, p.Observacao, nossoPreco = nosso });
    }

    public record PrecoLoteItem(Guid? ProdutoId, string Descricao, string? Ean, decimal Preco, string Unidade, string? Observacao);

    /// <summary>Coleta em massa: grava vários preços de um concorrente de uma vez.</summary>
    [HttpPost("concorrente/{concorrenteId:guid}/precos-lote")]
    public async Task<IActionResult> AdicionarPrecosLote(Guid concorrenteId,
        [FromBody] List<PrecoLoteItem> itens, CancellationToken ct)
    {
        var conc = await db.Concorrentes.FirstOrDefaultAsync(c => c.Id == concorrenteId, ct);
        if (conc is null) return NotFound();
        int add = 0;
        foreach (var i in itens ?? [])
        {
            if (string.IsNullOrWhiteSpace(i.Descricao) || i.Preco <= 0) continue;
            db.PrecosConcorrente.Add(PrecoConcorrente.Criar(conc.EmpresaId, concorrenteId,
                i.Descricao.Trim(), i.Preco, i.Unidade, i.ProdutoId, i.Ean, null, i.Observacao));
            add++;
        }
        if (add > 0) await db.SaveChangesAsync(ct);
        return Ok(new { adicionados = add });
    }

    [HttpDelete("precos/{id:guid}")]
    public async Task<IActionResult> RemoverPreco(Guid id, CancellationToken ct)
    {
        var p = await db.PrecosConcorrente.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        db.PrecosConcorrente.Remove(p);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Comparativo nosso × concorrentes por produto (loja): mín/méd/máx dos concorrentes.</summary>
    [HttpGet("comparativo/{localEstoqueId:guid}")]
    public async Task<IActionResult> Comparativo(Guid localEstoqueId, CancellationToken ct)
    {
        // Concorrentes ativos da loja.
        var concs = await db.Concorrentes.AsNoTracking()
            .Where(c => c.LocalEstoqueId == localEstoqueId && c.Ativo)
            .Select(c => new { c.Id, c.Nome })
            .ToListAsync(ct);
        var concIds = concs.Select(c => c.Id).ToList();
        var nomeConc = concs.ToDictionary(c => c.Id, c => c.Nome);

        // Preços coletados ligados a um produto nosso.
        var precos = await db.PrecosConcorrente.AsNoTracking()
            .Where(p => concIds.Contains(p.ConcorrenteId) && p.ProdutoId != null)
            .Select(p => new { p.ConcorrenteId, p.ProdutoId, p.Descricao, p.Preco, p.Unidade })
            .ToListAsync(ct);

        var produtoIds = precos.Select(p => p.ProdutoId!.Value).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(x => produtoIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Descricao, x.PrecoVenda, x.ProdutoBalanca, x.VendidoFracionado })
            .ToDictionaryAsync(x => x.Id, ct);

        // Normaliza o preço do concorrente para a MESMA base do nosso: granel (por peso)
        // = R$/kg (o nosso PrecoVenda de granel já é por kg); demais = por unidade.
        // Unidade incompatível (ex.: "un" num produto por peso) não entra no cálculo.
        static decimal? ParaBase(decimal preco, string? unid, bool porPeso)
        {
            var u = (unid ?? "").Trim().ToLowerInvariant();
            if (porPeso)
                return u switch { "kg" => preco, "100g" => preco * 10m, "g" => preco * 1000m, _ => (decimal?)null };
            return u switch { "un" or "" => preco, "dz" => Math.Round(preco / 12m, 2), "pct" => preco, _ => (decimal?)null };
        }

        var itens = precos.GroupBy(p => p.ProdutoId!.Value).Select(g =>
        {
            var prod = produtos.TryGetValue(g.Key, out var pr) ? pr : null;
            var porPeso = prod is not null && (prod.ProdutoBalanca || prod.VendidoFracionado);
            var baseUnidade = porPeso ? "kg" : "un";

            var precosConc = g.Select(x => new
            {
                concorrente = nomeConc.TryGetValue(x.ConcorrenteId, out var n) ? n : "?",
                precoBase = ParaBase(x.Preco, x.Unidade, porPeso),
                precoOrig = x.Preco, x.Unidade,
            }).ToList();
            var validos = precosConc.Where(x => x.precoBase != null)
                .Select(x => new { x.concorrente, preco = x.precoBase!.Value, x.precoOrig, x.Unidade })
                .OrderBy(x => x.preco).ToList();
            var incompativeis = precosConc.Count(x => x.precoBase == null);

            var nosso = prod?.PrecoVenda ?? 0m;
            var valores = validos.Select(x => x.preco).ToList();
            decimal min = valores.Count > 0 ? valores.Min() : 0m;
            decimal max = valores.Count > 0 ? valores.Max() : 0m;
            decimal med = valores.Count > 0 ? Math.Round(valores.Average(), 2) : 0m;
            return new
            {
                produtoId = g.Key,
                produto = prod?.Descricao ?? g.First().Descricao,
                baseUnidade, nossoPreco = nosso,
                concorrentes = validos, incompativeis, comparaveis = valores.Count,
                min, max, media = med,
                situacao = valores.Count == 0 ? "sem-comparavel"
                    : nosso <= 0 ? "sem-preco"
                    : nosso < min ? "mais-barato"
                    : nosso > max ? "mais-caro"
                    : "no-meio",
                difMenor = nosso > 0 && valores.Count > 0 ? Math.Round(nosso - min, 2) : 0m,
            };
        })
        .OrderByDescending(x => x.difMenor)   // onde estamos mais caros que o menor primeiro
        .ToList();

        return Ok(new { totalProdutos = itens.Count, itens });
    }

    /// <summary>
    /// Comparativo por NOME (genérico): busca "aveia em flocos", "castanha do pará", "quinoa"
    /// e junta o nosso preço com os preços coletados de concorrentes cujo nome bate — tudo
    /// normalizado (peso em R$/kg, senão R$/un). Não exige casar o SKU exato.
    /// </summary>
    [HttpGet("comparativo-nome")]
    public async Task<IActionResult> ComparativoNome([FromQuery] Guid empresaId,
        [FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(new { itens = Array.Empty<object>() });
        var termos = q.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var prodQ = db.Produtos.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Ativo);
        foreach (var t in termos) { var tt = t; prodQ = prodQ.Where(p => p.Descricao.Contains(tt)); }
        var nossos = await prodQ.OrderBy(p => p.Descricao).Take(40)
            .Select(p => new { p.Descricao, p.PrecoVenda, porPeso = p.ProdutoBalanca || p.VendidoFracionado })
            .ToListAsync(ct);

        var precoQ = db.PrecosConcorrente.AsNoTracking()
            .Join(db.Concorrentes, x => x.ConcorrenteId, c => c.Id, (x, c) => new { x, c })
            .Where(z => z.c.EmpresaId == empresaId && z.c.Ativo);
        foreach (var t in termos) { var tt = t; precoQ = precoQ.Where(z => z.x.Descricao.Contains(tt)); }
        var concs = await precoQ.OrderBy(z => z.x.Preco).Take(100)
            .Select(z => new { concorrente = z.c.Nome, z.x.Descricao, z.x.Preco, z.x.Unidade })
            .ToListAsync(ct);

        static (string unidadeBase, decimal precoBase) Normalizar(decimal preco, string? unidade, bool? porPesoProduto)
        {
            var u = (unidade ?? "").Trim().ToLowerInvariant();
            var peso = porPesoProduto ?? (u is "kg" or "100g" or "g");
            if (peso)
            {
                var pb = u switch { "kg" => preco, "100g" => preco * 10m, "g" => preco * 1000m, _ => preco };
                return ("kg", pb);
            }
            var pun = u == "dz" ? Math.Round(preco / 12m, 2) : preco;
            return ("un", pun);
        }

        var itens = new List<ItemNome>();
        foreach (var n in nossos)
        {
            var (b, pb) = Normalizar(n.PrecoVenda, n.porPeso ? "kg" : "un", n.porPeso);
            itens.Add(new ItemNome("Nós", true, n.Descricao, n.PrecoVenda, n.porPeso ? "kg" : "un", b, pb));
        }
        foreach (var c in concs)
        {
            var (b, pb) = Normalizar(c.Preco, c.Unidade, null);
            itens.Add(new ItemNome(c.concorrente, false, c.Descricao, c.Preco, c.Unidade ?? "un", b, pb));
        }
        // Preço de mercado online (Mercado Livre), quando conectado.
        if (ml.Configurado && await ml.ConectadoAsync(ct))
        {
            try
            {
                foreach (var m in await ml.BuscarAsync(q, 6, ct))
                    itens.Add(new ItemNome("Mercado Livre", false, m.Titulo, m.Preco, "un", "un", m.Preco));
            }
            catch { /* ML indisponível: segue sem ele */ }
        }
        var ordenado = itens.OrderBy(x => x.UnidadeBase).ThenBy(x => x.PrecoBase).ToList();
        return Ok(new { itens = ordenado });
    }

    private record ItemNome(string Fonte, bool EhNosso, string Descricao, decimal Preco,
        string Unidade, string UnidadeBase, decimal PrecoBase);

    // Normaliza um preço para a base (granel R$/kg, senão R$/un). porPesoProduto=null => decide pela unidade.
    private static (string unidadeBase, decimal precoBase) NormalizarPreco(decimal preco, string? unidade, bool? porPesoProduto)
    {
        var u = (unidade ?? "").Trim().ToLowerInvariant();
        var peso = porPesoProduto ?? (u is "kg" or "100g" or "g");
        if (peso)
            return ("kg", u switch { "kg" => preco, "100g" => preco * 10m, "g" => preco * 1000m, _ => preco });
        return ("un", u == "dz" ? Math.Round(preco / 12m, 2) : preco);
    }

    /// <summary>
    /// Histórico de preços coletados de um item (por nome): cada coleta é um ponto no tempo.
    /// Mostra a tendência por concorrente. Também devolve nosso preço de referência.
    /// </summary>
    [HttpGet("historico-nome")]
    public async Task<IActionResult> HistoricoNome([FromQuery] Guid empresaId,
        [FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(new { nossos = Array.Empty<object>(), historico = Array.Empty<object>() });
        var termos = q.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var prodQ = db.Produtos.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Ativo);
        foreach (var t in termos) { var tt = t; prodQ = prodQ.Where(p => p.Descricao.Contains(tt)); }
        var nossos = (await prodQ.OrderBy(p => p.Descricao).Take(20)
            .Select(p => new { p.Descricao, p.PrecoVenda, porPeso = p.ProdutoBalanca || p.VendidoFracionado })
            .ToListAsync(ct))
            .Select(n => { var (b, pb) = NormalizarPreco(n.PrecoVenda, n.porPeso ? "kg" : "un", n.porPeso);
                          return new { n.Descricao, n.PrecoVenda, unidadeBase = b, precoBase = pb }; })
            .ToList();

        var precoQ = db.PrecosConcorrente.AsNoTracking()
            .Join(db.Concorrentes, x => x.ConcorrenteId, c => c.Id, (x, c) => new { x, c })
            .Where(z => z.c.EmpresaId == empresaId);
        foreach (var t in termos) { var tt = t; precoQ = precoQ.Where(z => z.x.Descricao.Contains(tt)); }
        var rows = await precoQ.OrderBy(z => z.x.DataColeta)
            .Select(z => new { concorrente = z.c.Nome, z.x.Descricao, z.x.Preco, z.x.Unidade, z.x.DataColeta, z.x.ProdutoId })
            .ToListAsync(ct);

        var prodIds = rows.Where(r => r.ProdutoId != null).Select(r => r.ProdutoId!.Value).Distinct().ToList();
        var porPesoDic = await db.Produtos.AsNoTracking().Where(p => prodIds.Contains(p.Id))
            .Select(p => new { p.Id, peso = p.ProdutoBalanca || p.VendidoFracionado })
            .ToDictionaryAsync(p => p.Id, p => p.peso, ct);

        var historico = rows.Select(r =>
        {
            bool? peso = r.ProdutoId != null && porPesoDic.TryGetValue(r.ProdutoId.Value, out var pp) ? pp : null;
            var (b, pb) = NormalizarPreco(r.Preco, r.Unidade, peso);
            return new { r.concorrente, r.Descricao, r.Preco, r.Unidade, r.DataColeta, unidadeBase = b, precoBase = pb };
        }).ToList();

        return Ok(new { nossos, historico });
    }
}
