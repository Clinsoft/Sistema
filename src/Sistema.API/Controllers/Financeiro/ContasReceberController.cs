using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/contas-receber")]
[Authorize]
public class ContasReceberController(
    ILancamentoFinanceiroRepository repo,
    IContaBancariaRepository contaRepo,
    IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim,
        [FromQuery] string? status, CancellationToken ct)
    {
        var lancamentos = await repo.ListarPorPeriodoAsync(
            empresaId, TipoLancamento.ContaReceber,
            inicio ?? DateTime.Today.AddMonths(-1),
            fim ?? DateTime.Today.AddMonths(1), ct);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusLancamento>(status, out var st))
            lancamentos = lancamentos.Where(l => l.Status == st);

        return Ok(lancamentos.Select(l => new
        {
            l.Id, l.Descricao, l.ValorOriginal, l.ValorPago,
            saldo = l.Saldo, l.DataVencimento, l.DataPagamento,
            l.Status, l.Parcela, l.TotalParcelas,
            l.ClienteId, l.DocumentoOrigem, vencido = l.Vencido
        }));
    }

    [HttpGet("vencidas")]
    public async Task<IActionResult> Vencidas([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lancamentos = await repo.ListarVencidosAsync(empresaId, TipoLancamento.ContaReceber, ct);
        return Ok(new
        {
            total = lancamentos.Sum(l => l.Saldo),
            lancamentos
        });
    }

    [HttpGet("total-em-aberto")]
    public async Task<IActionResult> TotalEmAberto([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var total = await repo.TotalEmAbertoAsync(empresaId, TipoLancamento.ContaReceber, ct);
        return Ok(new { total });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarLancamentoRequest req, CancellationToken ct)
    {
        var grupo = Guid.NewGuid().ToString();
        var lancamentos = new List<LancamentoFinanceiro>();

        // Gera parcelas automaticamente
        for (int i = 1; i <= req.TotalParcelas; i++)
        {
            var vencimento = req.PrimeiroVencimento.AddMonths(i - 1);
            var valorParcela = Math.Round(req.Valor / req.TotalParcelas, 2);

            var l = LancamentoFinanceiro.Criar(req.EmpresaId, TipoLancamento.ContaReceber,
                req.Descricao, valorParcela, vencimento,
                clienteId: req.PessoaId, categoriaId: req.CategoriaId,
                contaBancariaId: req.ContaBancariaId,
                documentoOrigem: req.DocumentoOrigem,
                parcela: i, totalParcelas: req.TotalParcelas, grupoParcelamento: grupo);

            lancamentos.Add(l);
            await repo.AdicionarAsync(l, ct);
        }

        await uow.SalvarAsync(ct);
        return Ok(new { grupo, qtdParcelas = lancamentos.Count, ids = lancamentos.Select(l => l.Id) });
    }

    [HttpPost("{id:guid}/baixar")]
    public async Task<IActionResult> Baixar(Guid id, [FromBody] BaixarLancamentoRequest req, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");

        lancamento.Baixar(req.ValorPago, req.DataPagamento, req.ContaBancariaId);

        // Credita na conta bancária
        if (req.ContaBancariaId.HasValue)
        {
            var conta = await contaRepo.ObterPorIdAsync(req.ContaBancariaId.Value, ct);
            if (conta is not null)
            {
                conta.Creditar(req.ValorPago);
                contaRepo.Atualizar(conta);
            }
        }

        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        lancamento.Cancelar();
        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/renegociar")]
    public async Task<IActionResult> Renegociar(Guid id, [FromBody] RenegociarRequest req, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        lancamento.Renegociar(req.NovoValor, req.NovoVencimento, req.Observacao);
        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record CriarLancamentoRequest(
    Guid EmpresaId, string Descricao, decimal Valor,
    DateTime PrimeiroVencimento, int TotalParcelas = 1,
    Guid? PessoaId = null, Guid? CategoriaId = null,
    Guid? ContaBancariaId = null, string? DocumentoOrigem = null);

public record BaixarLancamentoRequest(decimal ValorPago, DateTime DataPagamento, Guid? ContaBancariaId = null);
public record RenegociarRequest(decimal NovoValor, DateTime NovoVencimento, string? Observacao = null);
