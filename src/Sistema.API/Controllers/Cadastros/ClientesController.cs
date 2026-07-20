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
        [FromQuery] bool incluirInativos = false,
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        if (incluirInativos)
        {
            var q = db.Clientes.AsNoTracking().Where(c => c.EmpresaId == empresaId);
            if (!string.IsNullOrWhiteSpace(termo))
                q = q.Where(c => c.Nome.Contains(termo)
                    || (c.CpfCnpj != null && c.CpfCnpj.Contains(termo))
                    || (c.Telefone != null && c.Telefone.Contains(termo)));

            var todos = await q.OrderBy(c => c.Nome)
                .Select(c => new
                {
                    c.Id, c.Nome, c.CpfCnpj, tipoPessoa = c.TipoPessoa.ToString(),
                    c.Email, c.Telefone, c.Celular, c.DataNascimento, c.Classificacao,
                    c.Logradouro, c.Numero, c.Complemento, c.Bairro, c.Cidade, c.Uf, c.Cep,
                    c.LimiteCredito, c.PontosFidelidade, c.Ativo, c.CriadoEm
                })
                .ToListAsync(ct);
            return Ok(new { itens = todos, total = todos.Count });
        }

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

    /// <summary>
    /// Garante que a pessoa (colaborador ou fornecedor) exista como cliente.
    /// Idempotente: se já houver cliente com o mesmo CPF/CNPJ, retorna o existente;
    /// caso contrário, cria com os dados básicos. Usado pela opção "também é cliente".
    /// </summary>
    [HttpPost("garantir")]
    public async Task<IActionResult> Garantir([FromBody] GarantirClienteRequest req, CancellationToken ct)
    {
        var doc = string.IsNullOrWhiteSpace(req.CpfCnpj) ? null : req.CpfCnpj.Trim();

        var existente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c =>
            c.EmpresaId == req.EmpresaId &&
            (doc != null ? c.CpfCnpj == doc : c.Nome == req.Nome), ct);
        if (existente is not null)
            return Ok(new { id = existente.Id, jaExistia = true });

        var tipo = (doc?.Length == 14) ? Domain.Cadastros.Entities.TipoPessoa.Juridica
                                       : Domain.Cadastros.Entities.TipoPessoa.Fisica;
        var cliente = Domain.Cadastros.Entities.Cliente.Criar(
            req.EmpresaId, req.Nome, tipo, doc, req.Email, req.Telefone);
        db.Clientes.Add(cliente);
        await uow.SalvarAsync(ct);
        return Ok(new { id = cliente.Id, jaExistia = false });
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

    [HttpPost("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        var cliente = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Cliente não encontrado.");
        cliente.Reativar();
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
public record GarantirClienteRequest(Guid EmpresaId, string Nome, string? CpfCnpj = null,
    string? Email = null, string? Telefone = null);
