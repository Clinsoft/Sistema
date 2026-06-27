using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/precos")]
[Authorize]
public class AtualizacaoPrecoController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    /// <summary>Atualiza preços em lote por percentual (positivo=aumento, negativo=redução).</summary>
    [HttpPost("reajuste-lote")]
    public async Task<IActionResult> ReajusteLote([FromBody] ReajusteLoteRequest req, CancellationToken ct)
    {
        var query = db.Produtos.Where(p => p.EmpresaId == req.EmpresaId && p.Ativo);

        if (req.CategoriaIds?.Any() == true)
            query = query.Where(p => req.CategoriaIds.Contains(p.CategoriaId));
        if (req.MarcaIds?.Any() == true)
            query = query.Where(p => req.MarcaIds.Contains(p.MarcaId));
        if (req.ProdutoIds?.Any() == true)
            query = query.Where(p => req.ProdutoIds.Contains(p.Id));

        var produtos = await query.ToListAsync(ct);
        var atualizados = 0;

        foreach (var p in produtos)
        {
            var novoPreco = req.TipoReajuste == "PERCENTUAL"
                ? Math.Round(p.PrecoVenda * (1 + req.Valor / 100), 2)
                : Math.Round(p.PrecoVenda + req.Valor, 2);

            if (novoPreco <= 0) continue;
            p.AtualizarPreco(p.CustoUnitario, novoPreco);
            db.Produtos.Update(p);
            atualizados++;
        }

        await uow.SalvarAsync(ct);
        return Ok(new { atualizados, totalSelecionados = produtos.Count });
    }

    /// <summary>Simulação de preço — retorna markup e margem para dado custo + preço.</summary>
    [HttpGet("calcular")]
    public IActionResult Calcular(
        [FromQuery] decimal custo, [FromQuery] decimal precoVenda,
        [FromQuery] decimal? impostos, [FromQuery] decimal? despesasFixas,
        [FromQuery] decimal? comissao)
    {
        if (custo <= 0 || precoVenda <= 0)
            throw new InvalidOperationException("Custo e preço devem ser positivos.");

        var markup = precoVenda / custo;
        var margemBruta = (precoVenda - custo) / precoVenda * 100;

        var custosAdicionais = (impostos ?? 0) + (despesasFixas ?? 0) + (comissao ?? 0);
        var margemLiquida = margemBruta - custosAdicionais;

        // Markup mínimo para cobrir custos e ter margem zero
        var markupMinimo = custosAdicionais > 0 && custosAdicionais < 100
            ? 1 / (1 - custosAdicionais / 100)
            : 1m;

        return Ok(new
        {
            custo, precoVenda, markup,
            margemBruta = Math.Round(margemBruta, 2),
            margemLiquida = Math.Round(margemLiquida, 2),
            markupMinimo = Math.Round(markupMinimo, 4),
            precoMinimoSugerido = Math.Round(custo * markupMinimo, 2)
        });
    }

    /// <summary>Pauta de preços — todos os produtos com custo, preço e margem.</summary>
    [HttpGet("pauta")]
    public async Task<IActionResult> Pauta([FromQuery] Guid empresaId,
        [FromQuery] Guid? categoriaId, CancellationToken ct)
    {
        var query = db.Produtos.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Ativo);
        if (categoriaId.HasValue) query = query.Where(p => p.CategoriaId == categoriaId);

        var produtos = await query
            .OrderBy(p => p.Descricao)
            .Select(p => new
            {
                p.Id, p.Codigo, p.Descricao,
                p.CustoUnitario, p.PrecoVenda, p.PrecoAtacado,
                p.Markup, p.MargemLucro
            })
            .ToListAsync(ct);

        return Ok(produtos);
    }
}

public record ReajusteLoteRequest(
    Guid EmpresaId,
    string TipoReajuste,   // PERCENTUAL | VALOR_FIXO
    decimal Valor,
    IList<Guid>? CategoriaIds = null,
    IList<Guid>? MarcaIds = null,
    IList<Guid>? ProdutoIds = null);
