using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

public class HistoricoMensagemWhatsApp : Entity
{
    public Guid   EmpresaId    { get; private set; }
    public Guid?  LocalEstoqueId { get; private set; }   // loja/unidade que disparou
    public Guid?  ClienteId    { get; private set; }
    public string Telefone     { get; private set; } = null!;
    public string NomeDestinatario { get; private set; } = null!;
    public TipoDisparoWhatsApp TipoDisparo { get; private set; }
    public string TemplateName { get; private set; } = null!;

    /// <summary>ID da mensagem retornado pela Meta (wam_id).</summary>
    public string? WamId       { get; private set; }
    public StatusMensagemWhatsApp Status { get; private set; }
    public string? ErroDetalhe { get; private set; }
    public DateTime EnviadoEm  { get; private set; }
    public DateTime? EntregueEm { get; private set; }
    public DateTime? LidoEm    { get; private set; }

    private HistoricoMensagemWhatsApp() { }

    public static HistoricoMensagemWhatsApp Criar(Guid empresaId, Guid? clienteId,
        string telefone, string nomeDestinatario,
        TipoDisparoWhatsApp tipoDisparo, string templateName, Guid? localEstoqueId = null)
        => new()
        {
            EmpresaId          = empresaId,
            LocalEstoqueId     = localEstoqueId,
            ClienteId          = clienteId,
            Telefone           = telefone,
            NomeDestinatario   = nomeDestinatario,
            TipoDisparo        = tipoDisparo,
            TemplateName       = templateName,
            Status             = StatusMensagemWhatsApp.Pendente,
            EnviadoEm          = DateTime.UtcNow,
        };

    public void MarcarEnviada(string wamId)
    {
        WamId  = wamId;
        Status = StatusMensagemWhatsApp.Enviada;
    }

    public void MarcarEntregue() { Status = StatusMensagemWhatsApp.Entregue; EntregueEm = DateTime.UtcNow; }
    public void MarcarLida()     { Status = StatusMensagemWhatsApp.Lida;     LidoEm     = DateTime.UtcNow; }
    public void MarcarFalha(string erro) { Status = StatusMensagemWhatsApp.Falhou; ErroDetalhe = erro; }
}

public enum StatusMensagemWhatsApp { Pendente, Enviada, Entregue, Lida, Falhou }
