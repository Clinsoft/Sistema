using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/contas-bancarias")]
[Authorize]
public class ContasBancariasController(IContaBancariaRepository repo, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await repo.ListarAtivasAsync(empresaId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var conta = await repo.ObterPorIdAsync(id, ct);
        return conta is null ? NotFound() : Ok(conta);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarContaBancariaRequest req, CancellationToken ct)
    {
        var tipo = Enum.Parse<TipoConta>(req.Tipo);
        var conta = ContaBancaria.Criar(req.EmpresaId, req.Nome, tipo, req.SaldoInicial,
            req.Banco, req.Agencia, req.NumeroConta, req.ContaPrincipal);
        await repo.AdicionarAsync(conta, ct);
        await uow.SalvarAsync(ct);
        return Ok(new { conta.Id, conta.SaldoAtual });
    }

    [HttpGet("{id:guid}/extrato")]
    public async Task<IActionResult> Extrato(Guid id, [FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var movs = await repo.ListarMovimentacoesAsync(empresaId, id, inicio, fim, ct);
        return Ok(new
        {
            periodo = new { inicio, fim },
            movimentos = movs,
            totalCreditos = movs.Where(m => m.Tipo == TipoMovimentacaoBancaria.Credito).Sum(m => m.Valor),
            totalDebitos = movs.Where(m => m.Tipo == TipoMovimentacaoBancaria.Debito).Sum(m => m.Valor)
        });
    }

    [HttpPost("{id:guid}/ajuste-saldo")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> AjustarSaldo(Guid id, [FromBody] AjusteSaldoRequest req, CancellationToken ct)
    {
        var conta = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Conta não encontrada.");
        conta.AjustarSaldo(req.NovoSaldo);
        repo.Atualizar(conta);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var conta = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Conta não encontrada.");
        conta.Desativar();
        repo.Atualizar(conta);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarContaBancariaRequest(
    Guid EmpresaId, string Nome, string Tipo, decimal SaldoInicial = 0,
    string? Banco = null, string? Agencia = null, string? NumeroConta = null,
    bool ContaPrincipal = false);

public record AjusteSaldoRequest(decimal NovoSaldo);
