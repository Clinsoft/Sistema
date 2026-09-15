using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Desempenho.Entities;

/// <summary>
/// Penalidade de desempenho por conversa do WhatsApp não respondida dentro do prazo
/// (6 horas de horário de funcionamento). Uma linha por colaborador penalizado e por
/// mensagem do cliente que ficou sem resposta — idempotente pela (ColaboradorId, MensagemId).
/// O desconto é subtraído da performance mensal do colaborador no cálculo da premiação.
/// </summary>
public class PenalidadeAtendimentoWhatsApp : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public Guid ColaboradorId { get; private set; }
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public DateTime DataOcorrencia { get; private set; }   // horário (local BRT) da mensagem sem resposta
    public string Telefone { get; private set; } = null!;
    public Guid MensagemId { get; private set; }           // mensagem recebida que ficou sem resposta
    public decimal Pontos { get; private set; }

    private PenalidadeAtendimentoWhatsApp() { }

    public static PenalidadeAtendimentoWhatsApp Criar(Guid empresaId, Guid localEstoqueId,
        Guid colaboradorId, DateTime dataOcorrenciaLocal, string telefone, Guid mensagemId, decimal pontos)
        => new()
        {
            EmpresaId = empresaId,
            LocalEstoqueId = localEstoqueId,
            ColaboradorId = colaboradorId,
            Ano = dataOcorrenciaLocal.Year,
            Mes = dataOcorrenciaLocal.Month,
            DataOcorrencia = dataOcorrenciaLocal,
            Telefone = telefone,
            MensagemId = mensagemId,
            Pontos = pontos,
        };
}
