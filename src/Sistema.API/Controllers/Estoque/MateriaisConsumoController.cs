using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Estoque;

/// <summary>
/// Materiais de consumo / uso interno (embalagens, sacolas, etiquetas…).
/// Cadastro e estoque separados dos produtos de venda: nada aqui aparece no
/// PDV, no catálogo ou na formação de preço.
/// </summary>
[ApiController]
[Route("api/materiais-consumo")]
[Authorize]
public class MateriaisConsumoController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    // ── Cadastro ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId, [FromQuery] string? termo,
        [FromQuery] bool? ativo = true, [FromQuery] bool abaixoMinimo = false,
        CancellationToken ct = default)
    {
        var query = db.MateriaisConsumo.AsNoTracking().Where(m => m.EmpresaId == empresaId);

        if (ativo.HasValue) query = query.Where(m => m.Ativo == ativo.Value);
        if (!string.IsNullOrWhiteSpace(termo))
            query = query.Where(m => m.Descricao.Contains(termo) || m.Codigo.Contains(termo)
                                  || (m.CodigoBarras != null && m.CodigoBarras.Contains(termo)));
        if (abaixoMinimo) query = query.Where(m => m.EstoqueAtual <= m.EstoqueMinimo);

        var itens = await query.OrderBy(m => m.Descricao)
            .Select(m => new
            {
                m.Id, m.Codigo, m.Descricao, m.UnidadeMedidaId, m.FornecedorPrincipalId,
                m.CodigoFornecedor, m.CodigoBarras, m.EstoqueAtual, m.EstoqueMinimo,
                m.CustoMedio, m.UltimoCusto, m.DataUltimaCompra, m.Localizacao,
                m.Observacao, m.Ativo, m.CriadoEm,
                UnidadeSigla = db.UnidadesMedida.Where(u => u.Id == m.UnidadeMedidaId)
                    .Select(u => u.Sigla).FirstOrDefault(),
                FornecedorNome = db.Fornecedores.Where(f => f.Id == m.FornecedorPrincipalId)
                    .Select(f => f.RazaoSocial).FirstOrDefault(),
                ValorEmEstoque = Math.Round(m.EstoqueAtual * m.CustoMedio, 2),
                AbaixoDoMinimo = m.EstoqueAtual <= m.EstoqueMinimo,
            })
            .ToListAsync(ct);

        return Ok(itens);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var m = await db.MateriaisConsumo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return m is null ? NotFound() : Ok(m);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] SalvarMaterialRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Descricao))
            return BadRequest(new { mensagem = "Informe a descrição do material." });

        // Código em branco → gera o próximo livre
        var codigo = string.IsNullOrWhiteSpace(req.Codigo)
            ? await ProximoCodigoAsync(req.EmpresaId, ct)
            : req.Codigo.Trim();

        if (await db.MateriaisConsumo.AnyAsync(m => m.EmpresaId == req.EmpresaId && m.Codigo == codigo, ct))
            return BadRequest(new { mensagem = $"Já existe material com o código '{codigo}'." });

        var material = MaterialConsumo.Criar(req.EmpresaId, codigo, req.Descricao.Trim(),
            req.UnidadeMedidaId, req.FornecedorPrincipalId, req.EstoqueMinimo);
        material.Editar(req.Descricao.Trim(), req.UnidadeMedidaId, req.FornecedorPrincipalId,
            req.EstoqueMinimo, req.Localizacao, req.Observacao, req.CodigoBarras, true);

        db.MateriaisConsumo.Add(material);
        await uow.SalvarAsync(ct);
        return CreatedAtAction(nameof(Obter), new { id = material.Id },
            new { id = material.Id, codigo = material.Codigo });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] SalvarMaterialRequest req, CancellationToken ct)
    {
        var material = await db.MateriaisConsumo.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Material não encontrado.");

        material.Editar(req.Descricao.Trim(), req.UnidadeMedidaId, req.FornecedorPrincipalId,
            req.EstoqueMinimo, req.Localizacao, req.Observacao, req.CodigoBarras, req.Ativo);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var material = await db.MateriaisConsumo.FindAsync([id], ct);
        if (material is null) return NoContent();

        if (await db.MovimentacoesMaterial.AnyAsync(mv => mv.MaterialConsumoId == id, ct))
            return BadRequest(new { mensagem = "Material com movimentações não pode ser excluído. Inative-o." });

        db.MateriaisConsumo.Remove(material);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpGet("proximo-codigo")]
    public async Task<IActionResult> ProximoCodigo([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(new { codigo = await ProximoCodigoAsync(empresaId, ct) });

    /// <summary>Códigos dos materiais são sequenciais a partir de 9001 (não colidem com produtos).</summary>
    private async Task<string> ProximoCodigoAsync(Guid empresaId, CancellationToken ct)
    {
        var codigos = await db.MateriaisConsumo.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId).Select(m => m.Codigo).ToListAsync(ct);
        var maior = codigos.Select(c => int.TryParse(c, out var n) ? n : 0).DefaultIfEmpty(9000).Max();
        return Math.Max(maior + 1, 9001).ToString();
    }

    // ── Movimentação ──────────────────────────────────────────────────────

    /// <summary>Entrada por compra manual (a entrada por NF-e vem da escrituração).</summary>
    [HttpPost("{id:guid}/entrada")]
    public async Task<IActionResult> Entrada(Guid id, [FromBody] EntradaMaterialRequest req, CancellationToken ct)
    {
        var material = await db.MateriaisConsumo.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Material não encontrado.");
        if (req.Quantidade <= 0) return BadRequest(new { mensagem = "Quantidade deve ser maior que zero." });

        material.EntradaEstoque(req.Quantidade, req.CustoUnitario, req.Data);
        db.MovimentacoesMaterial.Add(MovimentacaoMaterial.Criar(
            material.EmpresaId, id, TipoMovimentacaoMaterial.Entrada,
            req.Quantidade, req.CustoUnitario,
            req.DocumentoOrigem ?? "COMPRA", req.UsuarioId, req.Observacao));

        await uow.SalvarAsync(ct);
        return Ok(new { material.EstoqueAtual, material.CustoMedio, material.UltimoCusto });
    }

    /// <summary>Saída: consumo interno, produção ou perda.</summary>
    [HttpPost("{id:guid}/saida")]
    public async Task<IActionResult> Saida(Guid id, [FromBody] SaidaMaterialRequest req, CancellationToken ct)
    {
        var material = await db.MateriaisConsumo.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Material não encontrado.");
        if (req.Quantidade <= 0) return BadRequest(new { mensagem = "Quantidade deve ser maior que zero." });

        if (!Enum.TryParse<TipoMovimentacaoMaterial>(req.Tipo, true, out var tipo) ||
            tipo is not (TipoMovimentacaoMaterial.ConsumoInterno or TipoMovimentacaoMaterial.Producao
                      or TipoMovimentacaoMaterial.Perda))
            return BadRequest(new { mensagem = "Tipo de saída inválido (ConsumoInterno, Producao ou Perda)." });

        material.SaidaEstoque(req.Quantidade);
        db.MovimentacoesMaterial.Add(MovimentacaoMaterial.Criar(
            material.EmpresaId, id, tipo, req.Quantidade, material.CustoMedio,
            tipo.ToString().ToUpperInvariant(), req.UsuarioId, req.Observacao));

        await uow.SalvarAsync(ct);
        return Ok(new { material.EstoqueAtual, valorBaixado = Math.Round(req.Quantidade * material.CustoMedio, 2) });
    }

    /// <summary>Baixa de consumo em lote — o caso do dia a dia (usei 80 embalagens, 45 sacolas).</summary>
    [HttpPost("consumo-lote")]
    public async Task<IActionResult> ConsumoLote([FromBody] ConsumoLoteRequest req, CancellationToken ct)
    {
        if (req.Itens is null || req.Itens.Count == 0)
            return BadRequest(new { mensagem = "Informe os materiais consumidos." });

        if (!Enum.TryParse<TipoMovimentacaoMaterial>(req.Tipo ?? "ConsumoInterno", true, out var tipo))
            tipo = TipoMovimentacaoMaterial.ConsumoInterno;

        var resultados = new List<object>();
        foreach (var item in req.Itens)
        {
            var material = await db.MateriaisConsumo.FirstOrDefaultAsync(
                m => m.Id == item.MaterialConsumoId && m.EmpresaId == req.EmpresaId, ct);
            if (material is null || item.Quantidade <= 0) continue;

            material.SaidaEstoque(item.Quantidade);
            db.MovimentacoesMaterial.Add(MovimentacaoMaterial.Criar(
                req.EmpresaId, material.Id, tipo, item.Quantidade, material.CustoMedio,
                tipo.ToString().ToUpperInvariant(), req.UsuarioId, req.Observacao));

            resultados.Add(new
            {
                material.Id, material.Descricao, baixado = item.Quantidade,
                material.EstoqueAtual, valor = Math.Round(item.Quantidade * material.CustoMedio, 2)
            });
        }

        await uow.SalvarAsync(ct);
        return Ok(new { processados = resultados.Count, resultados });
    }

    /// <summary>Ajuste unitário — informa a quantidade física real.</summary>
    [HttpPost("{id:guid}/ajuste")]
    public async Task<IActionResult> Ajuste(Guid id, [FromBody] AjusteMaterialRequest req, CancellationToken ct)
    {
        var material = await db.MateriaisConsumo.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Material não encontrado.");

        var diferenca = req.QuantidadeContada - material.EstoqueAtual;
        if (diferenca == 0) return Ok(new { diferenca = 0m, mensagem = "Sem diferença." });

        material.AjustarEstoque(diferenca);
        db.MovimentacoesMaterial.Add(MovimentacaoMaterial.Criar(
            material.EmpresaId, id,
            diferenca > 0 ? TipoMovimentacaoMaterial.AjustePositivo : TipoMovimentacaoMaterial.AjusteNegativo,
            Math.Abs(diferenca), material.CustoMedio, "AJUSTE", req.UsuarioId, req.Observacao));

        await uow.SalvarAsync(ct);
        return Ok(new { diferenca, material.EstoqueAtual });
    }

    /// <summary>Inventário de materiais (em lote), separado do inventário de mercadorias.</summary>
    [HttpPost("inventario")]
    public async Task<IActionResult> Inventario([FromBody] InventarioMaterialRequest req, CancellationToken ct)
    {
        var resultados = new List<object>();
        var documento = $"INVENTARIO-{DateTime.Today:yyyyMMdd}";

        foreach (var item in req.Itens ?? [])
        {
            var material = await db.MateriaisConsumo.FirstOrDefaultAsync(
                m => m.Id == item.MaterialConsumoId && m.EmpresaId == req.EmpresaId, ct);
            if (material is null) continue;

            var diferenca = item.QuantidadeContada - material.EstoqueAtual;
            if (diferenca == 0) continue;

            material.AjustarEstoque(diferenca);
            db.MovimentacoesMaterial.Add(MovimentacaoMaterial.Criar(
                req.EmpresaId, material.Id,
                diferenca > 0 ? TipoMovimentacaoMaterial.AjustePositivo : TipoMovimentacaoMaterial.AjusteNegativo,
                Math.Abs(diferenca), material.CustoMedio, documento, req.UsuarioId, "Inventário de materiais"));

            resultados.Add(new { material.Id, material.Descricao, diferenca, material.EstoqueAtual });
        }

        await uow.SalvarAsync(ct);
        return Ok(new { ajustados = resultados.Count, resultados });
    }

    /// <summary>Extrato de movimentações de um material.</summary>
    [HttpGet("{id:guid}/movimentacoes")]
    public async Task<IActionResult> Movimentacoes(Guid id,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim, CancellationToken ct = default)
    {
        var query = db.MovimentacoesMaterial.AsNoTracking().Where(m => m.MaterialConsumoId == id);
        if (inicio.HasValue) query = query.Where(m => m.CriadoEm >= inicio.Value.Date);
        if (fim.HasValue) query = query.Where(m => m.CriadoEm < fim.Value.Date.AddDays(1));

        var movs = await query.OrderByDescending(m => m.CriadoEm)
            .Select(m => new
            {
                m.Id, Tipo = m.Tipo.ToString(), m.Quantidade, m.CustoUnitario,
                m.DocumentoOrigem, m.Observacao, m.CriadoEm,
                ValorTotal = Math.Round(m.Quantidade * m.CustoUnitario, 2),
            })
            .ToListAsync(ct);

        return Ok(movs);
    }
}

public record SalvarMaterialRequest(
    Guid EmpresaId, string? Codigo, string Descricao, Guid UnidadeMedidaId,
    Guid? FornecedorPrincipalId = null, decimal EstoqueMinimo = 0,
    string? Localizacao = null, string? Observacao = null,
    string? CodigoBarras = null, bool Ativo = true);

public record EntradaMaterialRequest(
    decimal Quantidade, decimal CustoUnitario, DateTime? Data = null,
    string? DocumentoOrigem = null, Guid? UsuarioId = null, string? Observacao = null);

public record SaidaMaterialRequest(
    decimal Quantidade, string Tipo = "ConsumoInterno",
    Guid? UsuarioId = null, string? Observacao = null);

public record ItemConsumo(Guid MaterialConsumoId, decimal Quantidade);

public record ConsumoLoteRequest(
    Guid EmpresaId, List<ItemConsumo> Itens, string? Tipo = "ConsumoInterno",
    Guid? UsuarioId = null, string? Observacao = null);

public record AjusteMaterialRequest(
    decimal QuantidadeContada, Guid? UsuarioId = null, string? Observacao = null);

public record ItemInventarioMaterial(Guid MaterialConsumoId, decimal QuantidadeContada);

public record InventarioMaterialRequest(
    Guid EmpresaId, List<ItemInventarioMaterial> Itens, Guid? UsuarioId = null);
