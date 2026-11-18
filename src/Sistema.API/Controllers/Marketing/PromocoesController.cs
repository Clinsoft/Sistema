using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Marketing.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Marketing;

[ApiController]
[Route("api/promocoes")]
[Authorize]
[Sistema.API.Auth.RequerRecurso(Sistema.Domain.Assinaturas.Recurso.Fidelidade)]
public class PromocoesController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Lista as promoções da empresa, com filtros de tipo, status (calculado) e texto.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] string? tipo,
        [FromQuery] string? status,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var query = db.Promocoes.AsNoTracking().Where(p => p.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(p => p.Tipo == tipo);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Nome.Contains(q));

        var lista = await query.OrderByDescending(p => p.CriadoEm).ToListAsync(ct);

        var resultado = lista.Select(p => new
        {
            p.Id, p.Nome, p.Tipo, p.TipoDesconto, p.Desconto,
            p.DataInicio, p.DataFim, p.AplicaEm, p.ReferenciaId,
            p.QtdeLeve, p.QtdePague, p.ValorMinimoPedido, p.LimiteUso,
            p.ApenasClube, p.Cumulativo, p.QtdeUsada, p.LocalEstoqueId,
            status = p.StatusCalculado(),
        });

        // Filtro por status (calculado) aplicado em memória
        if (!string.IsNullOrWhiteSpace(status))
            resultado = resultado.Where(x => x.status == status);

        return Ok(resultado.ToList());
    }

    /// <summary>Produtos de uma campanha (por prefixo do nome, ex.: "BLACK FRIDAY") prontos
    /// para imprimir etiqueta: traz o produto + o MELHOR preço já com o desconto da promoção.
    /// Usado pela tela de Etiquetas (modelo Black Friday) para só listar os produtos da campanha.</summary>
    [HttpGet("produtos-etiqueta")]
    public async Task<IActionResult> ProdutosEtiqueta(
        [FromQuery] Guid empresaId, [FromQuery] string termo = "BLACK FRIDAY", CancellationToken ct = default)
    {
        var promos = await db.Promocoes.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.AplicaEm == "Produto"
                && p.ReferenciaId != null && p.Nome.StartsWith(termo))
            .Select(p => new { ProdutoId = p.ReferenciaId!.Value, p.TipoDesconto, p.Desconto })
            .ToListAsync(ct);
        if (promos.Count == 0) return Ok(Array.Empty<object>());

        var ids = promos.Select(p => p.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo && ids.Contains(p.Id))
            .ToListAsync(ct);

        var resultado = produtos.Select(p =>
        {
            // Melhor (menor) preço entre as promoções Black Friday do produto.
            decimal? melhor = null;
            foreach (var pr in promos.Where(x => x.ProdutoId == p.Id))
            {
                var preco = pr.TipoDesconto == "ValorFixo"
                    ? Math.Max(0, p.PrecoVenda - pr.Desconto)
                    : Math.Round(p.PrecoVenda * (1 - pr.Desconto / 100m), 2);
                if (melhor is null || preco < melhor) melhor = preco;
            }
            return new
            {
                p.Id, p.Codigo, p.CodigoBarras, p.CodigoPlu, p.Descricao, p.DescricaoComplementar,
                p.PrecoVenda, precoBlackFriday = melhor ?? p.PrecoVenda,
                p.ProdutoBalanca, p.ImagemUrl, p.ValidadeEmDias
            };
        }).OrderBy(x => x.Descricao).ToList();

        return Ok(resultado);
    }

    /// <summary>Produtos que estão em promoção ATIVA com desconto automático (entidade Promocao,
    /// Tipo=Desconto, AplicaEm=Produto, dentro da vigência). Traz o produto + o MELHOR preço já
    /// com o desconto. Usado pela tela de Etiquetas (modelo EcoGranel Promoção).</summary>
    [HttpGet("produtos-promocao-ativa")]
    public async Task<IActionResult> ProdutosPromocaoAtiva(
        [FromQuery] Guid empresaId, CancellationToken ct = default)
    {
        var hoje = DateTime.UtcNow.Date;
        var promos = await db.Promocoes.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativa
                && p.Tipo == "Desconto" && p.AplicaEm == "Produto" && p.ReferenciaId != null
                && p.DataInicio <= hoje && (p.DataFim == null || p.DataFim >= hoje))
            .Select(p => new { ProdutoId = p.ReferenciaId!.Value, p.Nome, p.TipoDesconto, p.Desconto })
            .ToListAsync(ct);
        if (promos.Count == 0) return Ok(Array.Empty<object>());

        var ids = promos.Select(p => p.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo && ids.Contains(p.Id))
            .ToListAsync(ct);

        var resultado = produtos.Select(p =>
        {
            // Melhor (menor) preço entre as promoções ativas do produto.
            decimal? melhor = null;
            string? nomePromo = null;
            foreach (var pr in promos.Where(x => x.ProdutoId == p.Id))
            {
                var preco = pr.TipoDesconto == "ValorFixo"
                    ? Math.Max(0, p.PrecoVenda - pr.Desconto)
                    : Math.Round(p.PrecoVenda * (1 - pr.Desconto / 100m), 2);
                if (melhor is null || preco < melhor) { melhor = preco; nomePromo = pr.Nome; }
            }
            return new
            {
                p.Id, p.Codigo, p.CodigoBarras, p.CodigoPlu, p.Descricao, p.DescricaoComplementar,
                p.PrecoVenda, precoPromocao = melhor ?? p.PrecoVenda, nomePromocao = nomePromo,
                p.ProdutoBalanca, p.ImagemUrl, p.ValidadeEmDias
            };
        }).OrderBy(x => x.Descricao).ToList();

        return Ok(resultado);
    }

    /// <summary>Dados prontos para preencher uma mensagem de WhatsApp a partir da promoção:
    /// texto do desconto, nome e preços do produto, e a arte (Feed) já gerada.</summary>
    [HttpGet("{id:guid}/mensagem")]
    public async Task<IActionResult> DadosMensagem(Guid id, CancellationToken ct)
    {
        var promo = await db.Promocoes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null) return NotFound();

        var ptBR = new System.Globalization.CultureInfo("pt-BR");

        string? produtoNome = null;
        decimal? precoDE = null;
        if (promo.AplicaEm == "Produto" && promo.ReferenciaId is { } pid)
        {
            var prod = await db.Produtos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct);
            produtoNome = prod?.Descricao;
            precoDE = prod?.PrecoVenda;
        }
        decimal? precoPOR = precoDE is { } de
            ? (promo.TipoDesconto == "Percentual" ? de * (1 - promo.Desconto / 100m) : de - promo.Desconto)
            : null;

        var descontoTxt = promo.TipoDesconto == "Percentual"
            ? $"{promo.Desconto:0}% de desconto"
            : $"R$ {promo.Desconto.ToString("0.00", ptBR)} de desconto";

        // Arte Feed já gerada para esta promoção (usada como imagem do cabeçalho).
        var arteFeed = await db.ArtesMarketing.AsNoTracking()
            .Where(a => a.EmpresaId == promo.EmpresaId
                     && a.Nome == promo.Nome + " — Feed"
                     && a.UrlExportada != null)
            .OrderByDescending(a => a.CriadoEm)
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            nomePromocao = promo.Nome,
            descontoTxt,
            produtoNome,
            precoDE  = precoDE  is { } d1 ? $"R$ {d1.ToString("0.00", ptBR)}" : null,
            precoPOR = precoPOR is { } d2 ? $"R$ {d2.ToString("0.00", ptBR)}" : null,
            arteUrl  = arteFeed != null ? $"/api/marketing/artes/{arteFeed.Id}/exportar" : null,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] PromocaoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            return BadRequest(new { mensagem = "Nome é obrigatório." });

        var promo = Promocao.Criar(
            req.EmpresaId, req.Nome, req.Tipo ?? "Desconto", req.TipoDesconto ?? "Percentual",
            req.Desconto, ParseData(req.DataInicio) ?? DateTime.Today, ParseData(req.DataFim),
            req.AplicaEm ?? "Todos", req.ReferenciaId,
            req.QtdeLeve, req.QtdePague, req.ValorMinimoPedido, req.LimiteUso,
            req.ApenasClube, req.Cumulativo, req.LocalEstoqueId);

        db.Promocoes.Add(promo);
        await db.SaveChangesAsync(ct);
        return Ok(new { promo.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] PromocaoRequest req, CancellationToken ct)
    {
        var promo = await db.Promocoes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Promoção não encontrada.");

        promo.Editar(
            req.Nome, req.Tipo ?? "Desconto", req.TipoDesconto ?? "Percentual", req.Desconto,
            ParseData(req.DataInicio) ?? promo.DataInicio, ParseData(req.DataFim),
            req.AplicaEm ?? "Todos", req.ReferenciaId,
            req.QtdeLeve, req.QtdePague, req.ValorMinimoPedido, req.LimiteUso,
            req.ApenasClube, req.Cumulativo, req.LocalEstoqueId);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> AlterarStatus(Guid id, [FromBody] StatusRequest req, CancellationToken ct)
    {
        var promo = await db.Promocoes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Promoção não encontrada.");

        // "Ativa" reativa; qualquer outro valor (Encerrada) desativa
        promo.DefinirAtiva(string.Equals(req.Status, "Ativa", StringComparison.OrdinalIgnoreCase));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var promo = await db.Promocoes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Promoção não encontrada.");
        db.Promocoes.Remove(promo);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static DateTime? ParseData(string? s)
        => DateTime.TryParse(s, out var d) ? d : null;
}

public record PromocaoRequest(
    Guid EmpresaId,
    string Nome,
    string? Tipo,
    string? TipoDesconto,
    decimal Desconto,
    string? DataInicio,
    string? DataFim,
    string? AplicaEm,
    Guid? ReferenciaId,
    int QtdeLeve = 0,
    int QtdePague = 0,
    decimal ValorMinimoPedido = 0,
    int LimiteUso = 0,
    bool ApenasClube = false,
    bool Cumulativo = false,
    Guid? LocalEstoqueId = null);

public record StatusRequest(string Status);
