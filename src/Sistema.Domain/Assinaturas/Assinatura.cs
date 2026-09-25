using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Assinaturas;

/// <summary>
/// Assinatura de uma empresa (tenant). O <see cref="Plano"/> decide os RECURSOS liberados;
/// o <see cref="Status"/> + datas decidem se a empresa PODE usar o sistema.
/// Durante o trial os recursos efetivos são os do Profissional (valor cheio por 14 dias).
/// </summary>
public class Assinatura : Entity
{
    /// <summary>Dias de tolerância após o vencimento antes de bloquear.</summary>
    public const int DiasToleranciaPadrao = 5;
    public const int DiasTrialPadrao = 14;

    public Guid EmpresaId { get; private set; }
    public PlanoAssinatura Plano { get; private set; }
    public StatusAssinatura Status { get; private set; }
    public CicloCobranca Ciclo { get; private set; }

    /// <summary>Fim do período de avaliação (quando Status = Trial).</summary>
    public DateTime? TrialAte { get; private set; }

    /// <summary>Data da próxima cobrança (quando Ativa). Vencido + tolerância = bloqueio.</summary>
    public DateTime? ProximoVencimento { get; private set; }

    /// <summary>Lojas contratadas (plano Rede). Demais planos = 1.</summary>
    public int LojasContratadas { get; private set; } = 1;

    public int DiasTolerancia { get; private set; } = DiasToleranciaPadrao;

    /// <summary>Observação livre do gestor (motivo de bloqueio, acordo, etc.).</summary>
    public string? Observacao { get; private set; }

    // Integração com gateway de pagamento (Asaas).
    public string? AsaasCustomerId { get; private set; }
    public string? AsaasSubscriptionId { get; private set; }

    // Régua de lembretes (evita repetir o mesmo aviso todo dia).
    public string? UltimoLembrete { get; private set; }
    public DateTime? UltimoLembreteEm { get; private set; }
    public void RegistrarLembrete(string chave) { UltimoLembrete = chave; UltimoLembreteEm = DateTime.UtcNow; }

    private Assinatura() { }

    /// <summary>Cria a assinatura em TRIAL ao configurar a empresa (Setup).</summary>
    public static Assinatura CriarTrial(Guid empresaId, PlanoAssinatura planoEscolhido,
        CicloCobranca ciclo = CicloCobranca.Mensal, int diasTrial = DiasTrialPadrao)
        => new()
        {
            EmpresaId = empresaId,
            Plano = planoEscolhido,
            Ciclo = ciclo,
            Status = StatusAssinatura.Trial,
            TrialAte = DateTime.UtcNow.AddDays(diasTrial),
            LojasContratadas = 1,
        };

    /// <summary>Marca como paga/ativa e define o próximo vencimento.</summary>
    public void RegistrarPagamento(DateTime proximoVencimento)
    {
        Status = StatusAssinatura.Ativa;
        ProximoVencimento = proximoVencimento;
        UltimoLembrete = null;   // novo ciclo: os lembretes voltam a valer
    }

    public void TrocarPlano(PlanoAssinatura plano, int? lojasContratadas = null)
    {
        Plano = plano;
        if (plano == PlanoAssinatura.Rede && lojasContratadas is int n) LojasContratadas = Math.Max(1, n);
        else if (plano != PlanoAssinatura.Rede) LojasContratadas = 1;
    }

    public void VincularAsaas(string customerId, string subscriptionId)
    {
        AsaasCustomerId = customerId;
        AsaasSubscriptionId = subscriptionId;
    }

    public void DefinirCiclo(CicloCobranca ciclo) => Ciclo = ciclo;
    public void DefinirLojasContratadas(int lojas) => LojasContratadas = Math.Max(1, lojas);
    public void EstenderTrial(int dias) { TrialAte = (TrialAte ?? DateTime.UtcNow).AddDays(dias); Status = StatusAssinatura.Trial; }
    public void Bloquear(string? motivo = null) { Status = StatusAssinatura.Bloqueada; Observacao = motivo; }
    public void Reativar() { Status = StatusAssinatura.Ativa; }
    public void Cancelar(string? motivo = null) { Status = StatusAssinatura.Cancelada; Observacao = motivo; }

    /// <summary>Situação EFETIVA agora (deriva status + datas).</summary>
    public SituacaoAssinatura Situacao(DateTime? agora = null)
    {
        var hoje = agora ?? DateTime.UtcNow;
        switch (Status)
        {
            case StatusAssinatura.Cancelada: return SituacaoAssinatura.Cancelada;
            case StatusAssinatura.Bloqueada: return SituacaoAssinatura.Bloqueada;
            case StatusAssinatura.Trial:
                return hoje <= (TrialAte ?? hoje) ? SituacaoAssinatura.TrialAtivo : SituacaoAssinatura.TrialExpirado;
            case StatusAssinatura.Ativa:
                if (ProximoVencimento is not DateTime venc) return SituacaoAssinatura.Ativa;
                if (hoje <= venc) return SituacaoAssinatura.Ativa;
                return hoje <= venc.AddDays(DiasTolerancia) ? SituacaoAssinatura.EmTolerancia : SituacaoAssinatura.Bloqueada;
            default: return SituacaoAssinatura.Bloqueada;
        }
    }

    /// <summary>A empresa pode operar o sistema? (trial ativo, ativa ou em tolerância).</summary>
    public bool PodeUsar(DateTime? agora = null)
        => Situacao(agora) is SituacaoAssinatura.TrialAtivo or SituacaoAssinatura.Ativa or SituacaoAssinatura.EmTolerancia;

    /// <summary>Plano EFETIVO de recursos: no trial ativo vale o Profissional (valor cheio).</summary>
    public PlanoAssinatura PlanoEfetivo(DateTime? agora = null)
        => Situacao(agora) == SituacaoAssinatura.TrialAtivo ? PlanoAssinatura.Profissional : Plano;

    /// <summary>Recursos liberados agora (considerando o trial).</summary>
    public IReadOnlyCollection<Recurso> RecursosEfetivos(DateTime? agora = null)
        => PlanoCatalogo.Recursos(PlanoEfetivo(agora));

    public bool TemRecurso(Recurso recurso, DateTime? agora = null)
        => PodeUsar(agora) && PlanoCatalogo.Inclui(PlanoEfetivo(agora), recurso);
}
