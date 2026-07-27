using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>Geração da conta a pagar do DAS (Simples Nacional) a partir do faturamento do mês.</summary>
[ApiController]
[Route("api/das")]
[Authorize]
public class DasController(SistemaDbContext db) : ControllerBase
{
    public record GerarDasRequest(Guid EmpresaId, int Ano, int Mes, decimal Faturamento, decimal Aliquota);

    /// <summary>
    /// Lança o DAS da competência informada: valor = faturamento × alíquota efetiva.
    /// Categoria "Impostos", vencimento no dia 20 do mês seguinte (regra do Simples).
    /// Idempotente por competência.
    /// </summary>
    [HttpPost("gerar")]
    public async Task<IActionResult> Gerar([FromBody] GerarDasRequest req)
    {
        if (req.Faturamento <= 0 || req.Aliquota <= 0)
            return BadRequest(new { erro = "Faturamento e alíquota devem ser maiores que zero." });

        var competencia = new DateTime(req.Ano, req.Mes, 1);
        var docOrigem = $"DAS {competencia:yyyy-MM}";

        var existe = await db.LancamentosFinanceiros
            .AnyAsync(l => l.EmpresaId == req.EmpresaId && l.DocumentoOrigem == docOrigem);
        if (existe)
            return Conflict(new { erro = $"O DAS de {competencia:MM/yyyy} já foi gerado." });

        // Vencimento: dia 20 do mês seguinte à competência.
        var seguinte = competencia.AddMonths(1);
        var vencimento = new DateTime(seguinte.Year, seguinte.Month, 20);

        var valor = Math.Round(req.Faturamento * req.Aliquota / 100m, 2, MidpointRounding.AwayFromZero);

        // Beneficiário: fornecedor "Ministério da Fazenda - Simples Nacional", se cadastrado.
        var fornecedorId = await db.Fornecedores
            .Where(f => f.EmpresaId == req.EmpresaId && f.RazaoSocial.Contains("SIMPLES NACIONAL"))
            .Select(f => (Guid?)f.Id)
            .FirstOrDefaultAsync();

        var lanc = LancamentoFinanceiro.Criar(req.EmpresaId, TipoLancamento.ContaPagar,
            $"DAS Simples Nacional {competencia:MM/yyyy}", valor, vencimento,
            fornecedorId: fornecedorId, documentoOrigem: docOrigem);
        lanc.DefinirClassificacao("Impostos", "DAS/Simples Nacional",
            $"Faturamento R$ {req.Faturamento:N2} × {req.Aliquota:N2}% (Anexo I)");

        db.LancamentosFinanceiros.Add(lanc);
        await db.SaveChangesAsync();

        return Ok(new
        {
            competencia = competencia.ToString("yyyy-MM"),
            valor,
            vencimento = vencimento.ToString("yyyy-MM-dd")
        });
    }
}
