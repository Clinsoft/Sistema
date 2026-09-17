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
public class ConcorrentesController(SistemaDbContext db, MapaConcorrenciaService mapa) : ControllerBase
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
        List<MapaConcorrenciaService.ConcorrenteOsm> achados;
        try
        {
            achados = await mapa.BuscarConcorrentesAsync(loja.Latitude!.Value, loja.Longitude!.Value, raioMetros, ct);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { mensagem = "Falha ao consultar o mapa (OSM/Overpass): " + ex.Message });
        }

        // Remove a PRÓPRIA loja (o OSM às vezes tem a EcoGranel cadastrada): pelo nome
        // da empresa/loja ou por estar praticamente no mesmo ponto (<40 m).
        var nomeLoja = (loja.Nome ?? "").Trim();
        achados = achados.Where(a =>
            a.DistanciaKm > 0.04m
            && a.Nome.IndexOf("ecogranel", StringComparison.OrdinalIgnoreCase) < 0
            && (nomeLoja.Length < 4 || a.Nome.IndexOf(nomeLoja, StringComparison.OrdinalIgnoreCase) < 0))
            .ToList();

        // Upsert por (LocalEstoqueId, OsmRef): mantém os que continuam, atualiza dados,
        // insere novos. Não remove os que sumiram (evita perder anotações futuras).
        var existentes = await db.Concorrentes
            .Where(c => c.LocalEstoqueId == localEstoqueId && c.Fonte == "OSM")
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
                db.Concorrentes.Add(Concorrente.CriarDoOsm(loja.EmpresaId, localEstoqueId, a.Nome,
                    a.Categoria, a.Lat, a.Lng, a.DistanciaKm, a.Endereco, a.Telefone, a.Website, a.OsmRef));
                novos++;
            }
        }
        // Autoritativo p/ fonte OSM: desativa os que não vieram mais nesta varredura
        // (ex.: após restringir o filtro para só lojas de produtos naturais).
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
            raioKm, encontrados = achados.Count, novos, atualizados, removidos,
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
}
