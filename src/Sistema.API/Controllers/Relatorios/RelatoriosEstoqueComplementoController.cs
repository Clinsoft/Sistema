using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Relatorios;

[ApiController]
[Route("api/relatorios/estoque")]
[Authorize]
public class RelatoriosEstoqueComplementoController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Tributação dos produtos — NCM, CEST, CST, CSOSN.</summary>
    [HttpGet("tributacao")]
    public async Task<IActionResult> Tributacao([FromQuery] Guid empresaId,
        [FromQuery] Guid? categoriaId, CancellationToken ct)
    {
        var query = db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo);

        if (categoriaId.HasValue) query = query.Where(p => p.CategoriaId == categoriaId);

        var produtos = await query
            .Select(p => new
            {
                p.Id, p.Codigo, p.Descricao, p.Ncm, p.Cest,
                p.AliquotaIcms, p.AliquotaPis, p.AliquotaCofins,
                p.CstIcms, p.CsosnIcms, p.CstPisCofins, p.CategoriaId
            })
            .OrderBy(p => p.Ncm)
            .ThenBy(p => p.Descricao)
            .ToListAsync(ct);

        return Ok(new { total = produtos.Count, produtos });
    }

    /// <summary>Transferências por local de estoque.</summary>
    [HttpGet("transferencias")]
    public async Task<IActionResult> Transferencias([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        [FromQuery] Guid? localOrigemId, [FromQuery] Guid? localDestinoId,
        CancellationToken ct)
    {
        // Transferência = par de movimentações (Saida + Entrada) com DocumentoOrigem = "TRANSFERENCIA-{guid}"
        var movs = await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId
                && m.CriadoEm >= inicio && m.CriadoEm < fim.AddDays(1)
                && (m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Saida
                    || m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Entrada)
                && m.DocumentoOrigem != null && m.DocumentoOrigem.StartsWith("TRANSFERENCIA"))
            .Select(m => new
            {
                m.Id, m.ProdutoId, m.LocalEstoqueId, m.Tipo,
                m.Quantidade, m.DocumentoOrigem, dataHora = m.CriadoEm
            })
            .ToListAsync(ct);

        // Filtrar por local se solicitado
        var filtrado = movs.AsEnumerable();
        if (localOrigemId.HasValue)
            filtrado = filtrado.Where(m => m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Saida
                && m.LocalEstoqueId == localOrigemId);
        if (localDestinoId.HasValue)
            filtrado = filtrado.Where(m => m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Entrada
                && m.LocalEstoqueId == localDestinoId);

        return Ok(new
        {
            periodo = new { inicio, fim },
            movimentacoes = filtrado.OrderBy(m => m.dataHora),
            totalTransferencias = movs.Count(m => m.Tipo == Domain.Estoque.Entities.TipoMovimentacao.Saida)
        });
    }

    /// <summary>Estoque mínimo — produtos abaixo ou próximos do mínimo.</summary>
    [HttpGet("minimo")]
    public async Task<IActionResult> EstoqueMinimo([FromQuery] Guid empresaId,
        [FromQuery] bool apenasAbaixo = true, CancellationToken ct = default)
    {
        var query = db.Produtos.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Ativo);

        if (apenasAbaixo)
            query = query.Where(p => p.EstoqueAtual <= p.EstoqueMinimo);
        else
            query = query.Where(p => p.EstoqueAtual <= p.EstoqueMinimo * 1.2m); // até 20% acima

        var produtos = await query
            .OrderBy(p => p.EstoqueAtual - p.EstoqueMinimo)
            .Select(p => new
            {
                p.Id, p.Codigo, p.Descricao,
                p.EstoqueAtual, p.EstoqueMinimo,
                diferenca = p.EstoqueAtual - p.EstoqueMinimo,
                criticidade = p.EstoqueAtual == 0 ? "Zerado"
                    : p.EstoqueAtual < p.EstoqueMinimo ? "Abaixo do Mínimo"
                    : "Próximo do Mínimo"
            })
            .ToListAsync(ct);

        return Ok(new
        {
            total = produtos.Count,
            zerados = produtos.Count(p => p.criticidade == "Zerado"),
            abaixoMinimo = produtos.Count(p => p.criticidade == "Abaixo do Mínimo"),
            proximoMinimo = produtos.Count(p => p.criticidade == "Próximo do Mínimo"),
            produtos
        });
    }

    /// <summary>Análise de estoque × vendas por produto (Giro de estoque).</summary>
    [HttpGet("analise-giro")]
    public async Task<IActionResult> AnaliseGiro([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var vendas = await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.DataHora >= inicio && x.v.DataHora < fim.AddDays(1)
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .GroupBy(x => x.i.ProdutoId)
            .Select(g => new
            {
                produtoId = g.Key,
                qtdVendida = g.Sum(x => x.i.Quantidade),
                faturado = g.Sum(x => x.i.Total)
            })
            .ToListAsync(ct);

        var estoques = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo)
            .Select(p => new
            {
                p.Id, p.Codigo, p.Descricao, p.EstoqueAtual, p.CustoUnitario
            })
            .ToListAsync(ct);

        var resultado = estoques.Select(e =>
        {
            var venda = vendas.FirstOrDefault(v => v.produtoId == e.Id);
            var qtdVendida = venda?.qtdVendida ?? 0;
            var giro = e.EstoqueAtual > 0 ? (double)qtdVendida / (double)e.EstoqueAtual : 0;
            return new
            {
                e.Id, e.Codigo, e.Descricao, e.EstoqueAtual,
                estoqueEmReais = Math.Round(e.EstoqueAtual * e.CustoUnitario, 2),
                qtdVendida, faturado = venda?.faturado ?? 0,
                giro = Math.Round(giro, 2),
                classificacao = giro >= 2 ? "Alto Giro" : giro >= 0.5 ? "Médio Giro" : "Baixo Giro"
            };
        })
        .OrderByDescending(x => x.giro)
        .ToList();

        return Ok(new
        {
            periodo = new { inicio, fim },
            resultado,
            totalEstoqueEmReais = resultado.Sum(r => r.estoqueEmReais)
        });
    }

    /// <summary>Sugestão de compra: produtos abaixo do mínimo ou com cobertura baixa
    /// (dias de estoque) frente à venda média, com quantidade sugerida e agrupados
    /// por fornecedor. `dias` = janela de análise; `diasAlvo` = cobertura desejada.</summary>
    [HttpGet("sugestao-compra")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
    public async Task<IActionResult> SugestaoCompra([FromQuery] Guid empresaId,
        [FromQuery] int dias = 30, [FromQuery] int diasAlvo = 30, CancellationToken ct = default)
    {
        if (dias < 1) dias = 30;
        if (diasAlvo < 1) diasAlvo = 30;
        var desde = DateTime.Today.AddDays(-dias);

        var vendas = (await db.ItensVenda.AsNoTracking()
            .Join(db.Vendas, i => i.VendaId, v => v.Id, (i, v) => new { i, v })
            .Where(x => x.v.EmpresaId == empresaId
                && x.v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada
                && x.v.DataHora >= desde)
            .GroupBy(x => x.i.ProdutoId)
            .Select(g => new { ProdutoId = g.Key, Qtd = g.Sum(x => x.i.Quantidade) })
            .ToListAsync(ct))
            .ToDictionary(x => x.ProdutoId, x => x.Qtd);

        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo)
            .Select(p => new
            {
                p.Id, p.Codigo, p.Descricao, p.EstoqueAtual, p.EstoqueMinimo,
                p.EstoqueMaximo, p.CustoUnitario, p.FornecedorPrincipalId
            })
            .ToListAsync(ct);

        var fornecedores = await db.Fornecedores.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId)
            .ToDictionaryAsync(f => f.Id, f => f.RazaoSocial, ct);

        var itens = new List<object>();
        decimal custoTotal = 0m;
        foreach (var p in produtos)
        {
            var vendaPeriodo = vendas.TryGetValue(p.Id, out var q) ? q : 0m;
            var vendaDia = vendaPeriodo / dias;
            var abaixoMinimo = p.EstoqueMinimo > 0 && p.EstoqueAtual <= p.EstoqueMinimo;
            var coberturaDias = vendaDia > 0 ? (double)(p.EstoqueAtual / vendaDia) : (p.EstoqueAtual > 0 ? 9999 : 0);

            // Sugere quando está abaixo do mínimo, OU vende e a cobertura é menor que o alvo.
            var precisa = abaixoMinimo || (vendaDia > 0 && coberturaDias < diasAlvo);
            if (!precisa) continue;

            var alvoQtd = p.EstoqueMaximo > 0
                ? p.EstoqueMaximo
                : Math.Max(p.EstoqueMinimo, Math.Ceiling(vendaDia * diasAlvo));
            var sugerida = Math.Ceiling(alvoQtd - p.EstoqueAtual);
            if (sugerida <= 0) continue;

            var custoSug = Math.Round(sugerida * p.CustoUnitario, 2);
            custoTotal += custoSug;
            itens.Add(new
            {
                p.Id, p.Codigo, p.Descricao,
                estoqueAtual = p.EstoqueAtual,
                estoqueMinimo = p.EstoqueMinimo,
                vendaDia = Math.Round(vendaDia, 2),
                coberturaDias = coberturaDias >= 9999 ? (double?)null : Math.Round(coberturaDias, 1),
                abaixoMinimo,
                quantidadeSugerida = sugerida,
                custoUnitario = p.CustoUnitario,
                custoSugerido = custoSug,
                fornecedorId = p.FornecedorPrincipalId,
                fornecedor = p.FornecedorPrincipalId.HasValue
                    && fornecedores.TryGetValue(p.FornecedorPrincipalId.Value, out var fn)
                    ? fn : "(sem fornecedor)"
            });
        }

        return Ok(new { dias, diasAlvo, itens, custoTotal });
    }
}
