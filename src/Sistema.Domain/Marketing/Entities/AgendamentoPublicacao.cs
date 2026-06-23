using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Marketing.Entities;

public class AgendamentoPublicacao : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ArteMarketingId { get; private set; }
    public RedesSociais Rede { get; private set; }
    public DateTime DataHoraAgendada { get; private set; }
    public DateTime? DataHoraPublicado { get; private set; }
    public string? Legenda { get; private set; }
    public StatusAgendamento Status { get; private set; }
    public string? ErroPublicacao { get; private set; }

    private AgendamentoPublicacao() { }

    public static AgendamentoPublicacao Criar(Guid empresaId, Guid arteId,
        RedesSociais rede, DateTime dataHora, string? legenda = null)
        => new()
        {
            EmpresaId = empresaId,
            ArteMarketingId = arteId,
            Rede = rede,
            DataHoraAgendada = dataHora,
            Legenda = legenda,
            Status = StatusAgendamento.Agendado
        };

    public void MarcarPublicado() { Status = StatusAgendamento.Publicado; DataHoraPublicado = DateTime.UtcNow; }
    public void MarcarFalha(string erro) { Status = StatusAgendamento.Falhou; ErroPublicacao = erro; }
    public void Cancelar() => Status = StatusAgendamento.Cancelado;
}

public enum RedesSociais { Instagram, Facebook, WhatsAppStatus }
public enum StatusAgendamento { Agendado, Publicado, Falhou, Cancelado }
