using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sistema.Domain.Assinaturas;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;

namespace Sistema.API.Controllers;

/// <summary>
/// Assinatura da empresa logada — o frontend usa para montar menu/rotas (recursos liberados
/// por plano) e para bloquear o sistema quando a assinatura não está em dia.
/// </summary>
[ApiController]
[Route("api/minha-assinatura")]
[Authorize]
[Sistema.API.Auth.PermitirBloqueado]   // precisa abrir mesmo com assinatura bloqueada (tela de regularização)
public class AssinaturasController(SistemaDbContext db, AsaasService asaas, IMemoryCache cache) : ControllerBase
{
    /// <summary>Gera/retorna a fatura recorrente (Asaas) para o lojista assinar o plano.
    /// Funciona mesmo com assinatura vencida (é como o cliente regulariza).</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutDto dto, CancellationToken ct)
    {
        if (!asaas.Configurado)
            return StatusCode(503, new { mensagem = "Pagamento online ainda não configurado. Fale com o suporte." });

        var empresaId = EmpresaClaim();
        if (empresaId is null) return Unauthorized();

        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId.Value, ct);
        var assin = await db.Assinaturas.FirstOrDefaultAsync(a => a.EmpresaId == empresaId.Value, ct);
        if (empresa is null || assin is null) return NotFound(new { mensagem = "Assinatura não encontrada." });

        var plano = Enum.TryParse<PlanoAssinatura>(dto.Plano, true, out var p) ? p : assin.Plano;
        var ciclo = string.Equals(dto.Ciclo, "Anual", StringComparison.OrdinalIgnoreCase) ? CicloCobranca.Anual : CicloCobranca.Mensal;

        try
        {
            // Já tem assinatura no gateway e o plano/ciclo não mudou → devolve o link existente.
            if (!string.IsNullOrWhiteSpace(assin.AsaasSubscriptionId) && plano == assin.Plano && ciclo == assin.Ciclo)
            {
                var linkExistente = await asaas.ObterLinkFaturaAsync(assin.AsaasSubscriptionId!, ct);
                return Ok(new { link = linkExistente, jaExistia = true });
            }

            // Trocou de plano ou ciclo: a recorrência antiga sai ANTES de criar a nova. Se cancelar falhar, para aqui
            // e nada muda; se criar falhar, o cliente fica sem recorrência e tenta de novo. Nunca com duas cobrando.
            if (!string.IsNullOrWhiteSpace(assin.AsaasSubscriptionId))
            {
                await asaas.CancelarAssinaturaAsync(assin.AsaasSubscriptionId!, ct);
                assin.DesvincularAssinaturaAsaas();
                await db.SaveChangesAsync(CancellationToken.None);   // já cancelou lá: grava mesmo se a tela desistir
            }

            var customerId = assin.AsaasCustomerId
                ?? await asaas.CriarClienteAsync(empresa.NomeFantasia, empresa.Cnpj, empresa.Email, empresa.Telefone, ct);

            // Em dia: mantém o vencimento já pago e a diferença dos dias que faltam entra uma vez na próxima cobrança.
            // Senão, primeira cobrança ao fim do trial (se ainda em trial) ou hoje.
            var hoje = DateTime.Today;
            var pagoAte = assin.PagoAte(hoje);
            var diferenca = assin.DiferencaProporcional(plano, hoje);
            var venc = pagoAte is DateTime ate ? DateOnly.FromDateTime(ate)
                : assin.TrialAte is DateTime t && t > DateTime.UtcNow ? DateOnly.FromDateTime(t) : DateOnly.FromDateTime(hoje);
            var valor = PlanoCatalogo.Preco(plano, ciclo);
            var subId = await asaas.CriarAssinaturaAsync(customerId, valor, venc,
                ciclo == CicloCobranca.Anual ? "YEARLY" : "MONTHLY",
                $"Assinatura {PlanoCatalogo.Nome(plano)} — Natural Sistemas", ct);

            // Grava antes de mexer na cobrança: se cair aqui, a próxima tentativa cancela esta e recomeça.
            assin.VincularAsaas(customerId, subId);
            await db.SaveChangesAsync(CancellationToken.None);

            if (diferenca > 0)
            {
                try
                {
                    await asaas.SomarNaPrimeiraCobrancaAsync(subId, diferenca,
                        $"Assinatura {PlanoCatalogo.Nome(plano)} + diferença proporcional — Natural Sistemas", CancellationToken.None);
                }
                catch
                {
                    // Sem a diferença na cobrança a troca não vale: o plano fica como estava e o cliente tenta de novo.
                    await asaas.CancelarAssinaturaAsync(subId, CancellationToken.None);
                    assin.DesvincularAssinaturaAsaas();
                    await db.SaveChangesAsync(CancellationToken.None);
                    throw;
                }
            }

            assin.TrocarPlano(plano, dto.LojasContratadas);
            assin.DefinirCiclo(ciclo);
            await db.SaveChangesAsync(CancellationToken.None);
            cache.Remove($"assinatura:{empresaId.Value}");

            var link = await asaas.ObterLinkFaturaAsync(subId, ct);
            return Ok(new
            {
                link, valor, plano = plano.ToString(), ciclo = ciclo.ToString(),
                emDia = pagoAte is not null, vencimento = venc, diferenca, proximaCobranca = valor + diferenca,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { mensagem = "Falha ao gerar a cobrança.", detalhe = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Minha(CancellationToken ct)
    {
        var empresaId = EmpresaClaim();
        if (empresaId is null) return Unauthorized();

        var a = await db.Assinaturas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId.Value, ct);

        // Empresa sem assinatura (instalações antigas / EcoGranel) = acesso TOTAL, sem gate.
        if (a is null)
        {
            return Ok(new
            {
                temAssinatura = false,
                podeUsar = true,
                situacao = "Ativa",
                plano = "Ilimitado",
                planoEfetivo = "Ilimitado",
                status = "Ativa",
                recursos = Enum.GetNames<Recurso>(),
                limites = new { maxUsuarios = PlanoCatalogo.Ilimitado, maxLojas = PlanoCatalogo.Ilimitado },
                trialAte = (DateTime?)null,
                diasRestantesTrial = 0,
                proximoVencimento = (DateTime?)null,
            });
        }

        var agora = DateTime.UtcNow;
        var situacao = a.Situacao(agora);
        var planoEfetivo = a.PlanoEfetivo(agora);
        var diasTrial = a.Status == StatusAssinatura.Trial && a.TrialAte is DateTime t
            ? Math.Max(0, (int)Math.Ceiling((t - agora).TotalDays)) : 0;

        return Ok(new
        {
            temAssinatura = true,
            podeUsar = a.PodeUsar(agora),
            situacao = situacao.ToString(),
            plano = a.Plano.ToString(),
            planoEfetivo = planoEfetivo.ToString(),
            status = a.Status.ToString(),
            ciclo = a.Ciclo.ToString(),
            recursos = a.RecursosEfetivos(agora).Select(r => r.ToString()).ToArray(),
            limites = new
            {
                maxUsuarios = PlanoCatalogo.MaxUsuarios(planoEfetivo),
                maxLojas = PlanoCatalogo.MaxLojas(planoEfetivo, a.LojasContratadas),
            },
            trialAte = a.TrialAte,
            diasRestantesTrial = diasTrial,
            proximoVencimento = a.ProximoVencimento,
        });
    }

    private Guid? EmpresaClaim()
        => Guid.TryParse(User.FindFirst("empresaId")?.Value, out var id) ? id : null;
}

public record CheckoutDto(string? Plano = null, string? Ciclo = null, int? LojasContratadas = null);
