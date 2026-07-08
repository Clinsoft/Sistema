using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Marketing.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Marketing;

[ApiController]
[Route("api/clube")]
[Authorize]
public class ClubeController(SistemaDbContext db) : ControllerBase
{
    // ── Membros ──────────────────────────────────────────────────────────
    [HttpGet("membros")]
    public async Task<IActionResult> ListarMembros(
        [FromQuery] Guid empresaId, [FromQuery] string? q, [FromQuery] string? status,
        CancellationToken ct)
    {
        var query =
            from m in db.MembrosClube.AsNoTracking()
            join c in db.Clientes.AsNoTracking() on m.ClienteId equals c.Id
            where m.EmpresaId == empresaId
            select new { m, c.Nome, c.CpfCnpj };

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.m.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Nome.Contains(q) || (x.CpfCnpj != null && x.CpfCnpj.Contains(q)));

        var lista = await query.OrderBy(x => x.Nome).ToListAsync(ct);

        return Ok(lista.Select(x => new
        {
            x.m.Id, x.m.ClienteId,
            nome = x.Nome, cpf = x.CpfCnpj,
            x.m.Status, x.m.DataAdesao, x.m.Observacao,
            x.m.SaldoCashback, x.m.TotalCashback, x.m.TotalCompras,
        }));
    }

    [HttpPost("membros")]
    public async Task<IActionResult> CriarMembro([FromBody] MembroRequest req, CancellationToken ct)
    {
        if (req.ClienteId == Guid.Empty)
            return BadRequest(new { mensagem = "Selecione um cliente." });

        var cliente = await db.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == req.ClienteId && c.EmpresaId == req.EmpresaId, ct);
        if (cliente is null)
            return BadRequest(new { mensagem = "Cliente não encontrado." });

        if (await db.MembrosClube.AnyAsync(m => m.EmpresaId == req.EmpresaId && m.ClienteId == req.ClienteId, ct))
            return Conflict(new { mensagem = "Este cliente já é membro do clube." });

        var membro = MembroClube.Criar(req.EmpresaId, req.ClienteId,
            req.Status ?? "Ativo", ParseData(req.DataAdesao) ?? DateTime.Today, req.Observacao);
        db.MembrosClube.Add(membro);
        await db.SaveChangesAsync(ct);
        return Ok(new { membro.Id });
    }

    [HttpPut("membros/{id:guid}")]
    public async Task<IActionResult> EditarMembro(Guid id, [FromBody] MembroRequest req, CancellationToken ct)
    {
        var membro = await db.MembrosClube.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException("Membro não encontrado.");
        membro.Editar(req.Status ?? "Ativo", ParseData(req.DataAdesao) ?? membro.DataAdesao, req.Observacao);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("membros/{id:guid}/ajuste-cashback")]
    public async Task<IActionResult> AjustarCashback(Guid id, [FromBody] AjusteCashbackRequest req, CancellationToken ct)
    {
        var membro = await db.MembrosClube.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException("Membro não encontrado.");

        if (req.Valor <= 0) return BadRequest(new { mensagem = "Valor deve ser maior que zero." });
        if (string.IsNullOrWhiteSpace(req.Motivo)) return BadRequest(new { mensagem = "Informe o motivo." });

        var credito = string.Equals(req.Tipo, "Credito", StringComparison.OrdinalIgnoreCase);
        if (credito) membro.Creditar(req.Valor);
        else membro.Debitar(req.Valor);

        db.MovimentosCashback.Add(MovimentoCashback.Criar(
            membro.EmpresaId, membro.Id, membro.ClienteId,
            credito ? "Credito" : "Debito", req.Valor, req.Motivo));

        await db.SaveChangesAsync(ct);
        return Ok(new { membro.SaldoCashback });
    }

    // ── Cashback (extrato de movimentos) ─────────────────────────────────
    [HttpGet("cashback")]
    public async Task<IActionResult> ListarCashback([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lista = await (
            from mv in db.MovimentosCashback.AsNoTracking()
            join c in db.Clientes.AsNoTracking() on mv.ClienteId equals c.Id
            where mv.EmpresaId == empresaId
            orderby mv.Data descending
            select new { clienteNome = c.Nome, mv.Tipo, mv.Valor, mv.Motivo, mv.Data }
        ).Take(500).ToListAsync(ct);
        return Ok(lista);
    }

    // ── Histórico (cashback gerado por venda) ────────────────────────────
    [HttpGet("historico")]
    public async Task<IActionResult> ListarHistorico([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lista = await (
            from mv in db.MovimentosCashback.AsNoTracking()
            join c in db.Clientes.AsNoTracking() on mv.ClienteId equals c.Id
            where mv.EmpresaId == empresaId && mv.VendaNumero != null
            orderby mv.Data descending
            select new
            {
                clienteNome = c.Nome,
                vendaNumero = mv.VendaNumero,
                desconto = mv.DescontoUsado,
                cashbackGerado = mv.Tipo == "Credito" ? mv.Valor : 0m,
                mv.Data,
            }
        ).Take(500).ToListAsync(ct);
        return Ok(lista);
    }

    // ── Configuração ─────────────────────────────────────────────────────
    [HttpGet("config")]
    public async Task<IActionResult> ObterConfig([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesClube.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct)
            ?? ConfiguracaoClube.Padrao(empresaId);

        return Ok(new
        {
            cfg.PercentualCashback, validade = cfg.Validade, cfg.MinimoResgate,
            cfg.LimiteUsoPercent, cfg.DescontoMembro, cfg.AniversarianteDuplo,
            cfg.Ativo, cfg.NomeClubeExibicao,
        });
    }

    [HttpPut("config")]
    public async Task<IActionResult> SalvarConfig([FromBody] ConfigClubeRequest req, CancellationToken ct)
    {
        var cfg = await db.ConfiguracoesClube.FirstOrDefaultAsync(c => c.EmpresaId == req.EmpresaId, ct);
        if (cfg is null)
        {
            cfg = ConfiguracaoClube.Padrao(req.EmpresaId);
            db.ConfiguracoesClube.Add(cfg);
        }
        cfg.Atualizar(req.PercentualCashback, req.Validade, req.MinimoResgate,
            req.LimiteUsoPercent, req.DescontoMembro, req.AniversarianteDuplo,
            req.Ativo, req.NomeClubeExibicao ?? "Clube de Promoções");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static DateTime? ParseData(string? s)
        => DateTime.TryParse(s, out var d) ? d : null;
}

public record MembroRequest(
    Guid EmpresaId, Guid ClienteId, string? Status, string? DataAdesao, string? Observacao);

public record AjusteCashbackRequest(
    Guid EmpresaId, string Tipo, decimal Valor, string Motivo);

public record ConfigClubeRequest(
    Guid EmpresaId,
    decimal PercentualCashback,
    int Validade,
    decimal MinimoResgate,
    decimal LimiteUsoPercent,
    decimal DescontoMembro,
    bool AniversarianteDuplo,
    bool Ativo,
    string? NomeClubeExibicao);
