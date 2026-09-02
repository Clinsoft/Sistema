using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Marketing.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Marketing;

[ApiController]
[Route("api/cupons-sorteio")]
[Authorize]
public class CuponsSorteioController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Gera um cupom de sorteio para uma venda (registra os dados do cliente na urna).</summary>
    [HttpPost]
    public async Task<IActionResult> Gerar([FromBody] GerarCupomRequest req, CancellationToken ct)
    {
        var promo = await db.Promocoes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == req.PromocaoId && p.EmpresaId == req.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Sorteio não encontrado.");

        // Número sequencial por promoção (nº do cupom na urna).
        var ultimo = await db.CuponsSorteio.AsNoTracking()
            .Where(c => c.PromocaoId == req.PromocaoId)
            .MaxAsync(c => (int?)c.Numero, ct) ?? 0;
        var numero = ultimo + 1;

        var cupom = CupomSorteio.Criar(req.EmpresaId, req.PromocaoId, req.LocalEstoqueId,
            numero, req.ClienteId, req.NomeCliente, req.Telefone, req.VendaId, req.ValorCompra);
        db.CuponsSorteio.Add(cupom);
        await db.SaveChangesAsync(ct);

        return Ok(new { cupom.Id, numero, premio = promo.Nome, promocao = promo.Nome });
    }

    /// <summary>Lista os cupons de uma promoção (para conferência/exportação).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, [FromQuery] Guid promocaoId,
        CancellationToken ct)
    {
        var cupons = await db.CuponsSorteio.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.PromocaoId == promocaoId)
            .OrderBy(c => c.Numero)
            .Select(c => new { c.Id, c.Numero, c.NomeCliente, c.Telefone, c.ValorCompra, c.CriadoEm, c.Sorteado })
            .ToListAsync(ct);
        return Ok(cupons);
    }

    /// <summary>Sorteia um cupom aleatório (opcionalmente entre os ainda não sorteados) e o marca.</summary>
    [HttpPost("sortear")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<IActionResult> Sortear([FromBody] SortearRequest req, CancellationToken ct)
    {
        var q = db.CuponsSorteio.Where(c => c.EmpresaId == req.EmpresaId && c.PromocaoId == req.PromocaoId);
        if (req.ApenasNaoSorteados) q = q.Where(c => !c.Sorteado);

        var total = await q.CountAsync(ct);
        if (total == 0) return BadRequest(new { mensagem = "Nenhum cupom disponível para sorteio." });

        var skip = Random.Shared.Next(total);
        var cupom = await q.OrderBy(c => c.Numero).Skip(skip).FirstAsync(ct);
        cupom.MarcarSorteado();
        await db.SaveChangesAsync(ct);

        return Ok(new { cupom.Id, cupom.Numero, cupom.NomeCliente, cupom.Telefone, cupom.ValorCompra });
    }
}

public record GerarCupomRequest(
    Guid EmpresaId, Guid PromocaoId, Guid? LocalEstoqueId,
    Guid? ClienteId, string NomeCliente, string? Telefone,
    Guid? VendaId, decimal ValorCompra);

public record SortearRequest(Guid EmpresaId, Guid PromocaoId, bool ApenasNaoSorteados = true);
