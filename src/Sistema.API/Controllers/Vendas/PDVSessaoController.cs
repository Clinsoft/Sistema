using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Application.Vendas.Commands;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.API.Controllers.Vendas;

[ApiController]
[Route("api/pdv/sessoes")]
[Authorize]
public class PDVSessaoController(IMediator mediator, IPDVSessaoRepository repo) : ControllerBase
{
    /// <summary>Abre uma nova sessão de caixa.</summary>
    [HttpPost("abrir")]
    public async Task<IActionResult> Abrir([FromBody] AbrirSessaoCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Ok(new { id });
    }

    /// <summary>Fecha a sessão de caixa com o saldo contado.</summary>
    [HttpPost("{id:guid}/fechar")]
    public async Task<IActionResult> Fechar(Guid id, [FromBody] FecharRequest req, CancellationToken ct)
    {
        var resultado = await mediator.Send(new FecharSessaoCommand(id, req.SaldoFechamento, req.Observacao), ct);
        return Ok(resultado);
    }

    /// <summary>Retorna a sessão aberta do usuário atual.</summary>
    [HttpGet("aberta")]
    public async Task<IActionResult> SessaoAberta([FromQuery] Guid empresaId, [FromQuery] Guid? usuarioId, CancellationToken ct)
    {
        var uid = usuarioId
            ?? (Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var cid) ? cid : Guid.Empty);
        var sessao = await repo.ObterSessaoAbertaAsync(empresaId, uid, ct);
        if (sessao is null) return Ok(null);
        return Ok(new
        {
            sessao.Id,
            numero = 1,
            abertoEm = sessao.Abertura,
            sessao.UsuarioId, sessao.LocalEstoqueId,
            sessao.SaldoAbertura, sessao.TotalVendas,
            sessao.TotalSuprimentos, sessao.TotalSangrias,
            sessao.Status
        });
    }

    /// <summary>Lista sessões por período (para relatório de fechamentos).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        CancellationToken ct)
    {
        var sessoes = await repo.ListarPorPeriodoAsync(empresaId, inicio, fim, ct);
        return Ok(sessoes.Select(s => new
        {
            s.Id, s.UsuarioId, s.Abertura, s.Fechamento,
            s.SaldoAbertura, s.SaldoFechamento,
            s.TotalVendas, s.TotalSuprimentos, s.TotalSangrias,
            s.Status, s.ObservacaoFechamento
        }));
    }
}

public record FecharRequest(decimal SaldoFechamento, string? Observacao = null);
