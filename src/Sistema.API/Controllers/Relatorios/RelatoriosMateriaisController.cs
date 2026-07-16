using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

/// <summary>Relatórios de materiais de consumo / uso interno.</summary>
[ApiController]
[Route("api/relatorios/materiais")]
[Authorize]
public class RelatoriosMateriaisController(SistemaDbContext db) : ControllerBase
{
    private static readonly TipoMovimentacaoMaterial[] TiposSaida =
    [
        TipoMovimentacaoMaterial.ConsumoInterno,
        TipoMovimentacaoMaterial.Producao,
        TipoMovimentacaoMaterial.Perda,
    ];

    /// <summary>Estoque de materiais: saldo, custo e valor investido.</summary>
    [HttpGet("estoque")]
    public async Task<IActionResult> Estoque([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var itens = await db.MateriaisConsumo.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Ativo)
            .OrderBy(m => m.Descricao)
            .Select(m => new
            {
                m.Codigo, m.Descricao, m.EstoqueAtual, m.EstoqueMinimo,
                m.CustoMedio, m.UltimoCusto, m.Localizacao, m.DataUltimaCompra,
                UnidadeSigla = db.UnidadesMedida.Where(u => u.Id == m.UnidadeMedidaId)
                    .Select(u => u.Sigla).FirstOrDefault(),
                ValorEmEstoque = Math.Round(m.EstoqueAtual * m.CustoMedio, 2),
                AbaixoDoMinimo = m.EstoqueAtual <= m.EstoqueMinimo,
            })
            .ToListAsync(ct);

        return Ok(new
        {
            itens,
            totalItens = itens.Count,
            valorTotal = itens.Sum(i => i.ValorEmEstoque),
            abaixoMinimo = itens.Count(i => i.AbaixoDoMinimo),
        });
    }

    /// <summary>Consumo por período (saídas), por material.</summary>
    [HttpGet("consumo")]
    public async Task<IActionResult> Consumo([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var fimEx = fim.Date.AddDays(1);
        var dados = await (
            from mv in db.MovimentacoesMaterial.AsNoTracking()
            join m in db.MateriaisConsumo on mv.MaterialConsumoId equals m.Id
            where mv.EmpresaId == empresaId && mv.CriadoEm >= inicio.Date && mv.CriadoEm < fimEx
               && TiposSaida.Contains(mv.Tipo)
            select new { m.Codigo, m.Descricao, mv.Tipo, mv.Quantidade, mv.CustoUnitario }
        ).ToListAsync(ct);

        var porMaterial = dados
            .GroupBy(d => new { d.Codigo, d.Descricao })
            .Select(g => new
            {
                g.Key.Codigo, g.Key.Descricao,
                quantidade = g.Sum(x => x.Quantidade),
                valor = Math.Round(g.Sum(x => x.Quantidade * x.CustoUnitario), 2),
                consumoInterno = g.Where(x => x.Tipo == TipoMovimentacaoMaterial.ConsumoInterno).Sum(x => x.Quantidade),
                producao = g.Where(x => x.Tipo == TipoMovimentacaoMaterial.Producao).Sum(x => x.Quantidade),
                perda = g.Where(x => x.Tipo == TipoMovimentacaoMaterial.Perda).Sum(x => x.Quantidade),
            })
            .OrderByDescending(x => x.valor)
            .ToList();

        return Ok(new
        {
            itens = porMaterial,
            valorTotal = porMaterial.Sum(x => x.valor),
            periodo = new { inicio = inicio.Date, fim = fim.Date },
        });
    }

    /// <summary>Última compra de cada material (data, custo e fornecedor).</summary>
    [HttpGet("ultima-compra")]
    public async Task<IActionResult> UltimaCompra([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var materiais = await db.MateriaisConsumo.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Ativo)
            .Select(m => new
            {
                m.Id, m.Codigo, m.Descricao, m.UltimoCusto, m.CustoMedio, m.DataUltimaCompra,
                FornecedorNome = db.Fornecedores.Where(f => f.Id == m.FornecedorPrincipalId)
                    .Select(f => f.RazaoSocial).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var ultimas = await db.MovimentacoesMaterial.AsNoTracking()
            .Where(mv => mv.EmpresaId == empresaId && mv.Tipo == TipoMovimentacaoMaterial.Entrada)
            .GroupBy(mv => mv.MaterialConsumoId)
            .Select(g => new { MaterialId = g.Key, Ultima = g.Max(x => x.CriadoEm) })
            .ToListAsync(ct);

        var itens = materiais.Select(m => new
        {
            m.Codigo, m.Descricao, m.UltimoCusto, m.CustoMedio, m.FornecedorNome,
            dataUltimaCompra = m.DataUltimaCompra,
            ultimaEntrada = ultimas.FirstOrDefault(u => u.MaterialId == m.Id)?.Ultima,
        })
        .OrderByDescending(x => x.ultimaEntrada ?? DateTime.MinValue)
        .ToList();

        return Ok(new { itens });
    }

    /// <summary>Custo total investido em materiais (estoque atual) e compras do período.</summary>
    [HttpGet("custo-total")]
    public async Task<IActionResult> CustoTotal([FromQuery] Guid empresaId,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim, CancellationToken ct)
    {
        var emEstoque = await db.MateriaisConsumo.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Ativo)
            .SumAsync(m => m.EstoqueAtual * m.CustoMedio, ct);

        var qEntradas = db.MovimentacoesMaterial.AsNoTracking()
            .Where(mv => mv.EmpresaId == empresaId && mv.Tipo == TipoMovimentacaoMaterial.Entrada);
        var qSaidas = db.MovimentacoesMaterial.AsNoTracking()
            .Where(mv => mv.EmpresaId == empresaId && TiposSaida.Contains(mv.Tipo));

        if (inicio.HasValue)
        {
            qEntradas = qEntradas.Where(mv => mv.CriadoEm >= inicio.Value.Date);
            qSaidas = qSaidas.Where(mv => mv.CriadoEm >= inicio.Value.Date);
        }
        if (fim.HasValue)
        {
            var fimEx = fim.Value.Date.AddDays(1);
            qEntradas = qEntradas.Where(mv => mv.CriadoEm < fimEx);
            qSaidas = qSaidas.Where(mv => mv.CriadoEm < fimEx);
        }

        var comprado = await qEntradas.SumAsync(mv => mv.Quantidade * mv.CustoUnitario, ct);
        var consumido = await qSaidas.SumAsync(mv => mv.Quantidade * mv.CustoUnitario, ct);

        return Ok(new
        {
            valorEmEstoque = Math.Round(emEstoque, 2),
            valorComprado = Math.Round(comprado, 2),
            valorConsumido = Math.Round(consumido, 2),
        });
    }

    /// <summary>
    /// Giro dos materiais no período: consumo ÷ estoque médio.
    /// Giro alto = material que roda rápido; giro baixo = estoque parado.
    /// </summary>
    [HttpGet("giro")]
    public async Task<IActionResult> Giro([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var fimEx = fim.Date.AddDays(1);
        var dias = Math.Max(1, (fim.Date - inicio.Date).Days + 1);

        var materiais = await db.MateriaisConsumo.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Ativo)
            .Select(m => new { m.Id, m.Codigo, m.Descricao, m.EstoqueAtual, m.CustoMedio })
            .ToListAsync(ct);

        var saidas = await db.MovimentacoesMaterial.AsNoTracking()
            .Where(mv => mv.EmpresaId == empresaId && mv.CriadoEm >= inicio.Date && mv.CriadoEm < fimEx
                      && TiposSaida.Contains(mv.Tipo))
            .GroupBy(mv => mv.MaterialConsumoId)
            .Select(g => new { MaterialId = g.Key, Consumo = g.Sum(x => x.Quantidade) })
            .ToListAsync(ct);

        var itens = materiais.Select(m =>
        {
            var consumo = saidas.FirstOrDefault(s => s.MaterialId == m.Id)?.Consumo ?? 0m;
            // Estoque médio aproximado: saldo atual + metade do que saiu no período
            var estoqueMedio = m.EstoqueAtual + consumo / 2;
            var giro = estoqueMedio > 0 ? Math.Round(consumo / estoqueMedio, 2) : 0m;
            var consumoDiario = Math.Round(consumo / dias, 3);
            return new
            {
                m.Codigo, m.Descricao, m.EstoqueAtual, consumo, giro, consumoDiario,
                // Dias de cobertura no ritmo atual de consumo
                diasCobertura = consumoDiario > 0 ? Math.Round(m.EstoqueAtual / consumoDiario, 0) : (decimal?)null,
                valorConsumido = Math.Round(consumo * m.CustoMedio, 2),
            };
        })
        .OrderByDescending(x => x.giro)
        .ToList();

        return Ok(new { itens, periodo = new { inicio = inicio.Date, fim = fim.Date, dias } });
    }

    /// <summary>Materiais no ou abaixo do estoque mínimo (reposição).</summary>
    [HttpGet("abaixo-minimo")]
    public async Task<IActionResult> AbaixoMinimo([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var itens = await db.MateriaisConsumo.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Ativo && m.EstoqueAtual <= m.EstoqueMinimo)
            .OrderBy(m => m.Descricao)
            .Select(m => new
            {
                m.Codigo, m.Descricao, m.EstoqueAtual, m.EstoqueMinimo, m.CustoMedio, m.UltimoCusto,
                UnidadeSigla = db.UnidadesMedida.Where(u => u.Id == m.UnidadeMedidaId)
                    .Select(u => u.Sigla).FirstOrDefault(),
                FornecedorNome = db.Fornecedores.Where(f => f.Id == m.FornecedorPrincipalId)
                    .Select(f => f.RazaoSocial).FirstOrDefault(),
                Repor = m.EstoqueMinimo - m.EstoqueAtual,
                CustoReposicao = Math.Round((m.EstoqueMinimo - m.EstoqueAtual) * m.CustoMedio, 2),
            })
            .ToListAsync(ct);

        return Ok(new { itens, total = itens.Count, custoReposicao = itens.Sum(i => i.CustoReposicao) });
    }
}
