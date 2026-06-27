using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Contabilidade.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Contabilidade;

[ApiController]
[Route("api/contabilidade/plano-contas")]
[Authorize(Roles = "Administrador,Contador")]
public class PlanoContasController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var contas = await db.PlanoContas.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Ativo)
            .OrderBy(c => c.Codigo)
            .ToListAsync(ct);
        return Ok(contas);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarContaContabilRequest req, CancellationToken ct)
    {
        var duplicada = await db.PlanoContas
            .AnyAsync(c => c.EmpresaId == req.EmpresaId && c.Codigo == req.Codigo, ct);
        if (duplicada)
            throw new InvalidOperationException($"Código '{req.Codigo}' já existe no plano de contas.");

        var natureza = Enum.Parse<NaturezaConta>(req.Natureza);
        var tipo = Enum.Parse<TipoConta>(req.Tipo);

        var conta = ContaContabil.Criar(req.EmpresaId, req.Codigo, req.Nome,
            natureza, tipo, req.Nivel, req.ContaPaiId);

        db.PlanoContas.Add(conta);
        await uow.SalvarAsync(ct);
        return Ok(new { conta.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var conta = await db.PlanoContas.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Conta não encontrada.");
        conta.Desativar();
        db.PlanoContas.Update(conta);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Seed — cria plano de contas simplificado padrão CFC.</summary>
    [HttpPost("seed")]
    public async Task<IActionResult> SeedPadrao([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var jaExiste = await db.PlanoContas.AnyAsync(c => c.EmpresaId == empresaId, ct);
        if (jaExiste) throw new InvalidOperationException("Plano de contas já existe para esta empresa.");

        var contas = new List<ContaContabil>
        {
            ContaContabil.Criar(empresaId, "1", "ATIVO", NaturezaConta.Ativo, TipoConta.Sintetica, 1),
            ContaContabil.Criar(empresaId, "1.1", "ATIVO CIRCULANTE", NaturezaConta.Ativo, TipoConta.Sintetica, 2),
            ContaContabil.Criar(empresaId, "1.1.01", "DISPONIBILIDADES", NaturezaConta.Ativo, TipoConta.Sintetica, 3),
            ContaContabil.Criar(empresaId, "1.1.01.001", "CAIXA", NaturezaConta.Ativo, TipoConta.Analitica, 4),
            ContaContabil.Criar(empresaId, "1.1.01.002", "BANCOS C/C", NaturezaConta.Ativo, TipoConta.Analitica, 4),
            ContaContabil.Criar(empresaId, "1.1.02", "CONTAS A RECEBER", NaturezaConta.Ativo, TipoConta.Sintetica, 3),
            ContaContabil.Criar(empresaId, "1.1.02.001", "CLIENTES", NaturezaConta.Ativo, TipoConta.Analitica, 4),
            ContaContabil.Criar(empresaId, "1.1.03", "ESTOQUES", NaturezaConta.Ativo, TipoConta.Sintetica, 3),
            ContaContabil.Criar(empresaId, "1.1.03.001", "MERCADORIAS PARA REVENDA", NaturezaConta.Ativo, TipoConta.Analitica, 4),
            ContaContabil.Criar(empresaId, "2", "PASSIVO", NaturezaConta.Passivo, TipoConta.Sintetica, 1),
            ContaContabil.Criar(empresaId, "2.1", "PASSIVO CIRCULANTE", NaturezaConta.Passivo, TipoConta.Sintetica, 2),
            ContaContabil.Criar(empresaId, "2.1.01", "FORNECEDORES", NaturezaConta.Passivo, TipoConta.Analitica, 3),
            ContaContabil.Criar(empresaId, "2.1.02", "OBRIGAÇÕES FISCAIS", NaturezaConta.Passivo, TipoConta.Sintetica, 3),
            ContaContabil.Criar(empresaId, "2.1.02.001", "ICMS A RECOLHER", NaturezaConta.Passivo, TipoConta.Analitica, 4),
            ContaContabil.Criar(empresaId, "2.1.02.002", "PIS A RECOLHER", NaturezaConta.Passivo, TipoConta.Analitica, 4),
            ContaContabil.Criar(empresaId, "2.1.02.003", "COFINS A RECOLHER", NaturezaConta.Passivo, TipoConta.Analitica, 4),
            ContaContabil.Criar(empresaId, "3", "PATRIMÔNIO LÍQUIDO", NaturezaConta.PatrimonioLiquido, TipoConta.Sintetica, 1),
            ContaContabil.Criar(empresaId, "3.1", "CAPITAL SOCIAL", NaturezaConta.PatrimonioLiquido, TipoConta.Analitica, 2),
            ContaContabil.Criar(empresaId, "3.2", "RESERVAS DE LUCROS", NaturezaConta.PatrimonioLiquido, TipoConta.Analitica, 2),
            ContaContabil.Criar(empresaId, "4", "RECEITAS", NaturezaConta.Receita, TipoConta.Sintetica, 1),
            ContaContabil.Criar(empresaId, "4.1", "RECEITA BRUTA DE VENDAS", NaturezaConta.Receita, TipoConta.Analitica, 2),
            ContaContabil.Criar(empresaId, "4.2", "DEDUÇÕES DA RECEITA", NaturezaConta.Receita, TipoConta.Analitica, 2),
            ContaContabil.Criar(empresaId, "5", "CUSTOS", NaturezaConta.Custo, TipoConta.Sintetica, 1),
            ContaContabil.Criar(empresaId, "5.1", "CMV", NaturezaConta.Custo, TipoConta.Analitica, 2),
            ContaContabil.Criar(empresaId, "6", "DESPESAS", NaturezaConta.Despesa, TipoConta.Sintetica, 1),
            ContaContabil.Criar(empresaId, "6.1", "DESPESAS ADMINISTRATIVAS", NaturezaConta.Despesa, TipoConta.Sintetica, 2),
            ContaContabil.Criar(empresaId, "6.1.01", "ALUGUEL", NaturezaConta.Despesa, TipoConta.Analitica, 3),
            ContaContabil.Criar(empresaId, "6.1.02", "SALÁRIOS E ENCARGOS", NaturezaConta.Despesa, TipoConta.Analitica, 3),
            ContaContabil.Criar(empresaId, "6.1.03", "ENERGIA ELÉTRICA", NaturezaConta.Despesa, TipoConta.Analitica, 3),
            ContaContabil.Criar(empresaId, "6.2", "DESPESAS FINANCEIRAS", NaturezaConta.Despesa, TipoConta.Analitica, 2),
        };

        db.PlanoContas.AddRange(contas);
        await uow.SalvarAsync(ct);
        return Ok(new { criadas = contas.Count });
    }
}

public record CriarContaContabilRequest(
    Guid EmpresaId, string Codigo, string Nome,
    string Natureza, string Tipo, int Nivel,
    Guid? ContaPaiId = null);
