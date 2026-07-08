using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/contas-pagar")]
[Authorize]
public class ContasPagarController(
    ILancamentoFinanceiroRepository repo,
    IContaBancariaRepository contaRepo,
    SistemaDbContext db,
    IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim,
        [FromQuery] string? status, CancellationToken ct)
    {
        var lancamentos = await repo.ListarPorPeriodoAsync(
            empresaId, TipoLancamento.ContaPagar,
            inicio ?? DateTime.Today.AddMonths(-1),
            fim ?? DateTime.Today.AddMonths(1), ct);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusLancamento>(status, out var st))
            lancamentos = lancamentos.Where(l => l.Status == st);

        var lista = lancamentos.ToList();
        var fornecedorIds = lista.Where(l => l.FornecedorId.HasValue)
            .Select(l => l.FornecedorId!.Value).Distinct().ToList();
        var fornecedores = fornecedorIds.Any()
            ? await db.Fornecedores.AsNoTracking()
                .Where(f => fornecedorIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.RazaoSocial, ct)
            : new Dictionary<Guid, string>();

        return Ok(lista.Select(l => new
        {
            l.Id, l.Descricao, l.ValorOriginal, l.ValorPago,
            saldo = l.Saldo, l.DataVencimento, l.DataPagamento,
            status = l.Status.ToString(), l.Parcela, l.TotalParcelas, l.Observacao,
            categoria = l.Categoria, l.FornecedorId,
            fornecedorNome = l.FornecedorId.HasValue && fornecedores.TryGetValue(l.FornecedorId.Value, out var fn) ? fn : null,
            l.DocumentoOrigem, vencido = l.Vencido
        }));
    }

    [HttpGet("vencidas")]
    public async Task<IActionResult> Vencidas([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lancamentos = await repo.ListarVencidosAsync(empresaId, TipoLancamento.ContaPagar, ct);
        return Ok(new { total = lancamentos.Sum(l => l.Saldo), lancamentos });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarContaPagarRequest req, CancellationToken ct)
    {
        var grupo = Guid.NewGuid().ToString();
        var ids = new List<Guid>();

        for (int i = 1; i <= req.TotalParcelas; i++)
        {
            var vencimento = req.PrimeiroVencimento.AddMonths(i - 1);
            var valorParcela = Math.Round(req.Valor / req.TotalParcelas, 2);

            var l = LancamentoFinanceiro.Criar(req.EmpresaId, TipoLancamento.ContaPagar,
                req.Descricao, valorParcela, vencimento,
                fornecedorId: req.FornecedorId, categoriaId: req.CategoriaId,
                contaBancariaId: req.ContaBancariaId,
                documentoOrigem: req.DocumentoOrigem,
                parcela: i, totalParcelas: req.TotalParcelas, grupoParcelamento: grupo);

            l.DefinirClassificacao(req.Categoria, null, req.Observacao);

            await repo.AdicionarAsync(l, ct);
            ids.Add(l.Id);
        }

        await uow.SalvarAsync(ct);
        return Ok(new { grupo, qtdParcelas = req.TotalParcelas, ids });
    }

    [HttpPost("{id:guid}/pagar")]
    public async Task<IActionResult> Pagar(Guid id, [FromBody] BaixarLancamentoRequest req, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");

        lancamento.Baixar(req.ValorPago, req.DataPagamento, req.ContaBancariaId);

        if (req.ContaBancariaId.HasValue)
        {
            var conta = await contaRepo.ObterPorIdAsync(req.ContaBancariaId.Value, ct);
            if (conta is not null)
            {
                conta.Debitar(req.ValorPago);
                contaRepo.Atualizar(conta);
            }
        }

        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] EditarLancamentoRequest req, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        lancamento.Editar(req.Descricao, req.ValorOriginal, req.DataVencimento, req.Observacao, req.FornecedorId);
        lancamento.DefinirClassificacao(req.Categoria, lancamento.ClienteNome, req.Observacao);
        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/renegociar")]
    public async Task<IActionResult> Renegociar(Guid id, [FromBody] RenegociarPagarRequest req, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        lancamento.Renegociar(req.NovoValor, req.NovoVencimento, req.Motivo);
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
}

public record EditarLancamentoRequest(string Descricao, decimal ValorOriginal, DateTime DataVencimento, string? Observacao = null, Guid? FornecedorId = null, string? Categoria = null);
public record RenegociarPagarRequest(decimal NovoValor, DateTime NovoVencimento, string? Motivo = null);

public record CriarContaPagarRequest(
    Guid EmpresaId, string Descricao, decimal Valor,
    DateTime PrimeiroVencimento, int TotalParcelas = 1,
    Guid? FornecedorId = null, Guid? CategoriaId = null,
    Guid? ContaBancariaId = null, string? DocumentoOrigem = null,
    string? Categoria = null, string? Observacao = null);
