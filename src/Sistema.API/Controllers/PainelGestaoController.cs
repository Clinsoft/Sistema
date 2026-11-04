using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Assinaturas;

namespace Sistema.API.Controllers;

/// <summary>
/// Entrada do painel de gestão (Atenda Mais): servidor falando com servidor, sem login de usuário, protegida por
/// chave própria no cabeçalho X-Painel-Chave (config PainelGestao:Chave). Sem chave configurada, fica fechada.
/// O banco do Natural continua sendo a fonte dos dados; a cobrança continua vindo dos avisos do Asaas.
/// </summary>
[ApiController]
[Route("api/painel-gestao")]
[AllowAnonymous]
public class PainelGestaoController(GestaoAssinaturas gestao, IConfiguration config, ILogger<PainelGestaoController> logger) : ControllerBase
{
    private const int TamanhoMinimoDaChave = 32;
    private const int MaximoDiasDeTeste = 60;

    /// <summary>Null quando a chave confere; senão, a resposta de recusa.</summary>
    private IActionResult? Recusa()
    {
        var esperada = config["PainelGestao:Chave"] ?? "";
        if (esperada.Length < TamanhoMinimoDaChave)
        {
            logger.LogWarning("PainelGestao:Chave não configurada: pedido do painel recusado.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensagem = "Painel de gestão não configurado." });
        }
        var recebida = Request.Headers["X-Painel-Chave"].FirstOrDefault() ?? "";
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebida), Encoding.UTF8.GetBytes(esperada))
            ? null : Unauthorized();
    }

    [HttpGet("clientes")]
    public async Task<IActionResult> Clientes(CancellationToken ct)
    {
        if (Recusa() is { } recusa) return recusa;
        var lista = await gestao.ListarAsync(ct);
        return Ok(new { total = lista.Count, clientes = lista });
    }

    [HttpPost("{id:guid}/bloquear")]
    public Task<IActionResult> Bloquear(Guid id, [FromBody] MotivoDto dto, CancellationToken ct)
        => Aplicar(id, ct, a =>
        {
            if (string.IsNullOrWhiteSpace(dto.Motivo)) throw new ArgumentException("Informe o motivo do bloqueio.");
            a.Bloquear(dto.Motivo.Trim());
        });

    [HttpPost("{id:guid}/reativar")]
    public Task<IActionResult> Reativar(Guid id, CancellationToken ct)
        => Aplicar(id, ct, a => a.Reativar());

    [HttpPost("{id:guid}/pagar")]
    public Task<IActionResult> RegistrarPagamento(Guid id, [FromBody] PagarDto dto, CancellationToken ct)
        => Aplicar(id, ct, a =>
        {
            if (dto.ProximoVencimento is not DateTime venc || venc.Date <= DateTime.Today)
                throw new ArgumentException("Informe um próximo vencimento depois de hoje.");
            a.RegistrarPagamento(venc.Date);
        });

    [HttpPost("{id:guid}/trial")]
    public Task<IActionResult> EstenderTrial(Guid id, [FromBody] DiasDto dto, CancellationToken ct)
        => Aplicar(id, ct, a =>
        {
            if (a.Status != StatusAssinatura.Trial) throw new ArgumentException("Só dá para estender quem está em teste grátis.");
            if (dto.Dias is < 1 or > MaximoDiasDeTeste) throw new ArgumentException($"Informe de 1 a {MaximoDiasDeTeste} dias.");
            a.EstenderTrial(dto.Dias);
        });

    private async Task<IActionResult> Aplicar(Guid id, CancellationToken ct, Action<Assinatura> acao)
    {
        if (Recusa() is { } recusa) return recusa;
        try
        {
            var a = await gestao.AplicarAsync(id, acao, ct);
            if (a is null) return NotFound(new { mensagem = "Empresa sem assinatura." });
            return Ok(new
            {
                ok = true,
                nome = await gestao.NomeAsync(id, ct),
                status = a.Status.ToString(),
                situacao = a.Situacao().ToString(),
                plano = a.Plano.ToString(),
                trialAte = a.TrialAte,
                proximoVencimento = a.ProximoVencimento,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }
}
