using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Vendas;

/// <summary>
/// Detecção e cancelamento de VENDAS DUPLICADAS (mesma cesta re-lançada por causa do
/// bug de emissão que fazia o PDV mostrar erro). O cancelamento reverte TUDO que a
/// finalização gerou: estoque, recebível de cartão, cupom fiscal e pontos de fidelidade.
/// </summary>
[ApiController]
[Route("api/vendas")]
[Authorize(Roles = "Administrador")]
public class DuplicatasController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    /// <summary>
    /// Lista as vendas duplicadas no período: cestas idênticas (mesmo produto + mesma
    /// quantidade exata) em sequência com até 5 min entre si = re-lançamento. A 1ª de
    /// cada grupo é MANTER; as demais, CANCELAR. Confiança ALTA = tem item por peso
    /// (quantidade fracionada, impossível coincidir); REVISAR = só unidade.
    /// </summary>
    [HttpGet("duplicatas")]
    public async Task<IActionResult> Duplicatas(
        [FromQuery] Guid empresaId, [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        const string sql = @"
WITH fp AS (
 SELECT v.Id, v.Numero, v.DataHora, v.Total, v.NotaFiscalId,
  (SELECT STRING_AGG(CONVERT(varchar(36),i.ProdutoId)+':'+CONVERT(varchar(20),i.Quantidade),'|') WITHIN GROUP (ORDER BY i.ProdutoId) FROM ItensVenda i WHERE i.VendaId=v.Id) Fp,
  (SELECT STRING_AGG(i.Descricao+' '+CONVERT(varchar(20),CAST(i.Quantidade AS decimal(10,3))),' + ') FROM ItensVenda i WHERE i.VendaId=v.Id) Cesta,
  (SELECT MAX(CASE WHEN i.Quantidade<>ROUND(i.Quantidade,0) THEN 1 ELSE 0 END) FROM ItensVenda i WHERE i.VendaId=v.Id) Peso
 FROM Vendas v WHERE v.Status='Finalizada' AND v.EmpresaId={0} AND v.DataHora>={1} AND v.DataHora<{2}
   AND (v.Observacao IS NULL OR v.Observacao NOT LIKE '%[[]dup-ok]%')),
seq AS (SELECT *, DATEDIFF(SECOND, LAG(DataHora) OVER(PARTITION BY Fp ORDER BY DataHora), DataHora) gap FROM fp WHERE Fp IS NOT NULL),
flg AS (SELECT *, CASE WHEN gap IS NULL OR gap>300 THEN 1 ELSE 0 END nr FROM seq),
runs AS (SELECT *, SUM(nr) OVER(PARTITION BY Fp ORDER BY DataHora ROWS UNBOUNDED PRECEDING) rid FROM flg),
grp AS (SELECT Fp,rid,COUNT(*) n FROM runs GROUP BY Fp,rid HAVING COUNT(*)>1),
fin AS (SELECT r.*, ROW_NUMBER() OVER(PARTITION BY r.Fp,r.rid ORDER BY r.DataHora) rn, CAST(DENSE_RANK() OVER(ORDER BY r.Fp,r.rid) AS int) Grupo FROM runs r JOIN grp g ON g.Fp=r.Fp AND g.rid=r.rid)
SELECT Grupo, Id AS VendaId, Numero,
  CONVERT(varchar(16),DataHora,120) AS DataHora, Total,
  CASE WHEN rn=1 THEN 'MANTER' ELSE 'CANCELAR' END AS Acao,
  CASE WHEN Peso=1 THEN 'ALTA' ELSE 'REVISAR' END AS Confianca,
  CAST(CASE WHEN EXISTS(SELECT 1 FROM NotasFiscais n WHERE n.Id=fin.NotaFiscalId AND n.Status=2) THEN 1 ELSE 0 END AS bit) AS NotaAutorizada,
  LEFT(ISNULL(Cesta,''),80) AS Cesta
FROM fin ORDER BY Grupo, rn;";

        var lista = await db.Database
            .SqlQueryRaw<DuplicataDto>(sql, empresaId, inicio, fim)
            .ToListAsync(ct);

        return Ok(new
        {
            grupos = lista.Select(x => x.Grupo).Distinct().Count(),
            aCancelar = lista.Count(x => x.Acao == "CANCELAR"),
            valorACancelar = lista.Where(x => x.Acao == "CANCELAR").Sum(x => x.Total),
            comNotaAutorizada = lista.Count(x => x.Acao == "CANCELAR" && x.NotaAutorizada),
            itens = lista,
        });
    }

    /// <summary>
    /// Cancela as vendas informadas revertendo TUDO, uma a uma (atômico por venda —
    /// se uma falhar, as outras seguem). Reverte: estoque (devolve), recebível de
    /// cartão (cancela), pontos de fidelidade (retira) e cupom fiscal (cancela interno;
    /// se estava AUTORIZADO, entra na lista para o contador cancelar na SEFAZ).
    /// </summary>
    [HttpPost("cancelar-lote")]
    public async Task<IActionResult> CancelarLote([FromBody] CancelarLoteRequest req, CancellationToken ct)
    {
        if (req.VendaIds is null || req.VendaIds.Count == 0)
            return BadRequest(new { mensagem = "Nenhuma venda selecionada." });

        var motivo = string.IsNullOrWhiteSpace(req.Motivo)
            ? "Venda duplicada (bug de emissão) — cancelada em lote" : req.Motivo.Trim();

        var cancelados = 0;
        var pulados = new List<string>();
        var erros = new List<object>();
        var notasParaContador = new List<object>();

        foreach (var id in req.VendaIds.Distinct())
        {
            try
            {
                var venda = await db.Vendas.Include(v => v.Itens)
                    .FirstOrDefaultAsync(v => v.Id == id, ct);
                if (venda is null) { pulados.Add($"{id} (não encontrada)"); continue; }
                if (venda.Status != StatusVenda.Finalizada) { pulados.Add($"{venda.Numero} (status {venda.Status})"); continue; }

                // 1) Estoque: devolve o que a venda baixou.
                foreach (var item in venda.Itens)
                {
                    var prod = await db.Produtos.FindAsync([item.ProdutoId], ct);
                    db.MovimentacoesEstoque.Add(MovimentacaoEstoque.Criar(
                        venda.EmpresaId, item.ProdutoId, venda.LocalEstoqueId,
                        TipoMovimentacao.Devolucao, item.Quantidade,
                        prod?.CustoUnitario ?? 0m, documentoOrigem: $"CANC-DUP-{venda.Numero}"));
                    prod?.AjustarEstoque(item.Quantidade);
                }

                // 2) Recebíveis de cartão gerados pela venda.
                var recs = await db.ReceiveisCartao
                    .Where(r => r.VendaId == venda.Id && r.Status != StatusRecebivelCartao.Cancelado)
                    .ToListAsync(ct);
                foreach (var r in recs) r.Cancelar();

                // 3) Cupom fiscal vinculado.
                if (venda.NotaFiscalId is Guid nid)
                {
                    var nota = await db.NotasFiscais.FindAsync([nid], ct);
                    if (nota is not null && nota.Status != StatusNF.Cancelada)
                    {
                        var eraAutorizada = nota.Status == StatusNF.Autorizada;
                        nota.Cancelar("DUPLICADA");   // Protocolo é nvarchar(20) — texto curto
                        if (eraAutorizada)
                            notasParaContador.Add(new { venda.Numero, chave = nota.ChaveAcesso, nota.Protocolo });
                    }
                }

                // 4) Pontos de fidelidade concedidos (1 ponto por real).
                if (venda.ClienteId is Guid cid)
                {
                    var cli = await db.Clientes.FindAsync([cid], ct);
                    cli?.RetirarPontos((int)Math.Floor(venda.Total));
                }

                // 5) A própria venda.
                venda.Cancelar(motivo);

                await uow.SalvarAsync(ct);
                cancelados++;
            }
            catch (Exception ex)
            {
                db.ChangeTracker.Clear();   // descarta alterações parciais desta venda
                erros.Add(new { id, erro = ex.Message });
            }
        }

        return Ok(new { cancelados, pulados, erros, notasParaContador });
    }

    /// <summary>
    /// Marca vendas como "revisadas" na detecção de duplicatas (não são duplicata ou já
    /// foram tratadas) para NÃO reaparecerem na lista. Só volta a mostrar duplicata nova.
    /// </summary>
    [HttpPost("duplicatas/ignorar")]
    public async Task<IActionResult> IgnorarDuplicatas([FromBody] IgnorarDuplicatasRequest req, CancellationToken ct)
    {
        if (req.VendaIds is null || req.VendaIds.Count == 0)
            return BadRequest(new { mensagem = "Nada para marcar." });

        var vendas = await db.Vendas.Where(v => req.VendaIds.Contains(v.Id)).ToListAsync(ct);
        foreach (var v in vendas) v.MarcarDuplicataRevisada();
        await uow.SalvarAsync(ct);
        return Ok(new { marcadas = vendas.Count });
    }
}

public class DuplicataDto
{
    public int Grupo { get; set; }
    public Guid VendaId { get; set; }
    public string Numero { get; set; } = "";
    public string DataHora { get; set; } = "";
    public decimal Total { get; set; }
    public string Acao { get; set; } = "";
    public string Confianca { get; set; } = "";
    public bool NotaAutorizada { get; set; }
    public string Cesta { get; set; } = "";
}

public record CancelarLoteRequest(List<Guid> VendaIds, string? Motivo = null);
public record IgnorarDuplicatasRequest(List<Guid> VendaIds);
