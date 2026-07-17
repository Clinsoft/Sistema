using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

/// <summary>
/// Ativo imobilizado: bens da empresa (balança, PDV, móveis, veículos).
/// Não é vendido nem consumido — controle por valor, localização e depreciação.
/// </summary>
[ApiController]
[Route("api/ativos-imobilizados")]
[Authorize]
public class AtivosImobilizadosController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId, [FromQuery] string? termo,
        [FromQuery] bool? ativo = true, [FromQuery] string? categoria = null,
        CancellationToken ct = default)
    {
        var query = db.AtivosImobilizados.AsNoTracking().Where(a => a.EmpresaId == empresaId);
        if (ativo.HasValue) query = query.Where(a => a.Ativo == ativo.Value);
        if (!string.IsNullOrWhiteSpace(termo))
            query = query.Where(a => a.Descricao.Contains(termo) || a.Codigo.Contains(termo)
                                  || (a.NumeroSerie != null && a.NumeroSerie.Contains(termo)));
        if (!string.IsNullOrWhiteSpace(categoria) && Enum.TryParse<CategoriaAtivo>(categoria, true, out var cat))
            query = query.Where(a => a.Categoria == cat);

        var bens = await query.OrderBy(a => a.Descricao).ToListAsync(ct);

        // Depreciação é calculada no domínio (não dá para traduzir para SQL)
        var itens = bens.Select(a => new
        {
            a.Id, a.Codigo, a.Descricao, Categoria = a.Categoria.ToString(),
            a.FornecedorPrincipalId, a.NotaFiscal, a.NumeroSerie, a.Localizacao,
            a.DataAquisicao, a.ValorAquisicao, a.Quantidade,
            a.VidaUtilMeses, a.ValorResidual, a.DataBaixa, a.MotivoBaixa,
            a.Observacao, a.Ativo,
            FornecedorNome = db.Fornecedores.Where(f => f.Id == a.FornecedorPrincipalId)
                .Select(f => f.RazaoSocial).FirstOrDefault(),
            a.DepreciacaoMensal,
            MesesDepreciados = a.MesesDepreciados(),
            DepreciacaoAcumulada = a.DepreciacaoAcumulada(),
            ValorContabil = a.ValorContabil(),
        }).ToList();

        return Ok(new
        {
            itens,
            total = itens.Count,
            valorAquisicao = itens.Sum(i => i.ValorAquisicao),
            valorContabil = itens.Sum(i => i.ValorContabil),
            depreciacaoAcumulada = itens.Sum(i => i.DepreciacaoAcumulada),
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var a = await db.AtivosImobilizados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? NotFound() : Ok(a);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] SalvarAtivoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Descricao))
            return BadRequest(new { mensagem = "Informe a descrição do bem." });

        var codigo = string.IsNullOrWhiteSpace(req.Codigo)
            ? await ProximoCodigoAsync(req.EmpresaId, ct)
            : req.Codigo.Trim();

        if (await db.AtivosImobilizados.AnyAsync(a => a.EmpresaId == req.EmpresaId && a.Codigo == codigo, ct))
            return BadRequest(new { mensagem = $"Já existe bem com o código '{codigo}'." });

        Enum.TryParse<CategoriaAtivo>(req.Categoria ?? "Equipamento", true, out var cat);
        var ativo = AtivoImobilizado.Criar(req.EmpresaId, codigo, req.Descricao.Trim(),
            req.ValorAquisicao, req.DataAquisicao ?? DateTime.Today, cat,
            req.FornecedorPrincipalId, req.Quantidade);
        ativo.Editar(req.Descricao.Trim(), cat, req.FornecedorPrincipalId, req.ValorAquisicao,
            req.DataAquisicao ?? DateTime.Today, req.Quantidade, req.VidaUtilMeses,
            req.ValorResidual, req.NumeroSerie, req.Localizacao, req.Observacao, true);

        db.AtivosImobilizados.Add(ativo);
        await uow.SalvarAsync(ct);
        return CreatedAtAction(nameof(Obter), new { id = ativo.Id }, new { id = ativo.Id, codigo = ativo.Codigo });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] SalvarAtivoRequest req, CancellationToken ct)
    {
        var ativo = await db.AtivosImobilizados.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Bem não encontrado.");

        Enum.TryParse<CategoriaAtivo>(req.Categoria ?? "Equipamento", true, out var cat);
        ativo.Editar(req.Descricao.Trim(), cat, req.FornecedorPrincipalId, req.ValorAquisicao,
            req.DataAquisicao ?? ativo.DataAquisicao, req.Quantidade, req.VidaUtilMeses,
            req.ValorResidual, req.NumeroSerie, req.Localizacao, req.Observacao, req.Ativo);

        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Baixa do bem (venda, descarte, perda) — mantém o histórico.</summary>
    [HttpPost("{id:guid}/baixar")]
    public async Task<IActionResult> Baixar(Guid id, [FromBody] BaixarAtivoRequest req, CancellationToken ct)
    {
        var ativo = await db.AtivosImobilizados.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Bem não encontrado.");
        if (string.IsNullOrWhiteSpace(req.Motivo))
            return BadRequest(new { mensagem = "Informe o motivo da baixa." });

        try { ativo.Baixar(req.Data ?? DateTime.Today, req.Motivo); }
        catch (InvalidOperationException e) { return BadRequest(new { mensagem = e.Message }); }

        await uow.SalvarAsync(ct);
        return Ok(new { ativo.DataBaixa, valorContabilNaBaixa = ativo.ValorContabil() });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var ativo = await db.AtivosImobilizados.FindAsync([id], ct);
        if (ativo is null) return NoContent();

        if (await db.EntradasNFe.AnyAsync(e => e.Itens.Any(i => i.AtivoImobilizadoId == id), ct))
            return BadRequest(new { mensagem = "Bem vinculado a uma NF-e não pode ser excluído. Use a baixa." });

        db.AtivosImobilizados.Remove(ativo);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Bens ativos por categoria, com valor de aquisição e contábil.</summary>
    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var bens = await db.AtivosImobilizados.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.Ativo).ToListAsync(ct);

        var porCategoria = bens.GroupBy(a => a.Categoria)
            .Select(g => new
            {
                categoria = g.Key.ToString(),
                quantidade = g.Count(),
                valorAquisicao = g.Sum(a => a.ValorAquisicao),
                valorContabil = g.Sum(a => a.ValorContabil()),
                depreciacaoAcumulada = g.Sum(a => a.DepreciacaoAcumulada()),
            })
            .OrderByDescending(x => x.valorContabil)
            .ToList();

        return Ok(new
        {
            porCategoria,
            total = bens.Count,
            valorAquisicao = bens.Sum(a => a.ValorAquisicao),
            valorContabil = bens.Sum(a => a.ValorContabil()),
            depreciacaoAcumulada = bens.Sum(a => a.DepreciacaoAcumulada()),
            depreciacaoMensal = bens.Sum(a => a.DepreciacaoMensal),
        });
    }

    /// <summary>Códigos dos bens são sequenciais a partir de 7001.</summary>
    private async Task<string> ProximoCodigoAsync(Guid empresaId, CancellationToken ct)
    {
        var codigos = await db.AtivosImobilizados.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId).Select(a => a.Codigo).ToListAsync(ct);
        var maior = codigos.Select(c => int.TryParse(c, out var n) ? n : 0).DefaultIfEmpty(7000).Max();
        return Math.Max(maior + 1, 7001).ToString();
    }
}

public record SalvarAtivoRequest(
    Guid EmpresaId, string? Codigo, string Descricao, decimal ValorAquisicao,
    DateTime? DataAquisicao = null, string? Categoria = "Equipamento",
    Guid? FornecedorPrincipalId = null, decimal Quantidade = 1,
    int VidaUtilMeses = 0, decimal ValorResidual = 0,
    string? NumeroSerie = null, string? Localizacao = null,
    string? Observacao = null, bool Ativo = true);

public record BaixarAtivoRequest(string Motivo, DateTime? Data = null);
