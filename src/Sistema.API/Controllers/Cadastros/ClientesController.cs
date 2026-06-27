using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Cadastros.Commands;
using Sistema.Application.Cadastros.Queries;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClientesController(IMediator mediator, IClienteRepository repo,
    SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId, [FromQuery] string? termo,
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        var resultado = await mediator.Send(new ListarClientesQuery(empresaId, termo, pagina, tamanhoPagina), ct);
        return Ok(resultado);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarClienteCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarClienteRequest req, CancellationToken ct)
    {
        var cliente = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Cliente não encontrado.");
        cliente.AtualizarDados(req.Nome, req.Email, req.Telefone, req.Celular,
            req.Logradouro, req.Numero, req.Complemento, req.Bairro,
            req.Cidade, req.Uf, req.Cep, req.LimiteCredito, req.Classificacao);
        repo.Atualizar(cliente);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var cliente = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Cliente não encontrado.");
        cliente.Desativar();
        repo.Atualizar(cliente);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar(
        [FromQuery] Guid empresaId, [FromQuery] string? q, CancellationToken ct = default)
    {
        var resultado = await mediator.Send(new ListarClientesQuery(empresaId, q, 1, 20), ct);
        return Ok(resultado.Itens);
    }

    [HttpGet("{id:guid}/historico-compras")]
    public async Task<IActionResult> HistoricoCompras(Guid id,
        [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var vendas = await db.Vendas.AsNoTracking()
            .Include(v => v.Itens)
            .Include(v => v.Pagamentos)
            .Where(v => v.EmpresaId == empresaId && v.ClienteId == id
                && v.Status == Domain.Vendas.Entities.StatusVenda.Finalizada)
            .OrderByDescending(v => v.DataHora)
            .Select(v => new
            {
                v.Id, v.Numero, v.DataHora, v.Total,
                qtdItens = v.Itens.Count,
                formas = v.Pagamentos.Select(p => p.Forma.ToString())
            })
            .ToListAsync(ct);

        return Ok(new
        {
            clienteId = id,
            totalGasto = vendas.Sum(v => v.Total),
            qtdCompras = vendas.Count,
            vendas
        });
    }

    [HttpGet("{id:guid}/pontos")]
    public async Task<IActionResult> SaldoPontos(Guid id, CancellationToken ct)
    {
        var cliente = await repo.ObterPorIdAsync(id, ct);
        if (cliente is null) return NotFound();
        return Ok(new
        {
            clienteId = id,
            pontos = cliente.PontosFidelidade,
            valorEquivalente = Math.Round(cliente.PontosFidelidade / 100m, 2)
        });
    }

    [HttpPost("{id:guid}/pontos/resgatar")]
    public async Task<IActionResult> ResgatarPontos(Guid id, [FromBody] ResgatarPontosRequest req,
        [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var desconto = await mediator.Send(new ResgatarPontosCommand(empresaId, id, req.Pontos), ct);
        return Ok(new { pontosResgatados = req.Pontos, descontoGerado = desconto });
    }
}

public record AtualizarClienteRequest(
    string Nome, string? Email = null, string? Telefone = null, string? Celular = null,
    string? Logradouro = null, string? Numero = null, string? Complemento = null,
    string? Bairro = null, string? Cidade = null, string? Uf = null, string? Cep = null,
    decimal LimiteCredito = 0, string? Classificacao = null);

public record ResgatarPontosRequest(int Pontos);
