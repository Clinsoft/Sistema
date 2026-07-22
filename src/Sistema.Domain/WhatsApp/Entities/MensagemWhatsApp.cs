using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

/// <summary>
/// Mensagem de conversa do WhatsApp (caixa de entrada). Guarda tanto as mensagens
/// recebidas dos clientes (via webhook) quanto as respostas enviadas pelo atendente
/// (mensagens de sessão, texto livre dentro da janela de 24h).
/// </summary>
public class MensagemWhatsApp : Entity
{
    public Guid   EmpresaId   { get; private set; }
    public string Telefone    { get; private set; } = null!;   // wa_id (só dígitos)
    public string? NomeContato { get; private set; }           // nome do perfil do cliente
    public DirecaoMensagemWhatsApp Direcao { get; private set; }
    public string Texto       { get; private set; } = null!;
    public string Tipo        { get; private set; } = "text";  // text/image/audio/document/...
    public string? WamId      { get; private set; }
    public DateTime DataHora  { get; private set; }
    public bool   Lida        { get; private set; }

    private MensagemWhatsApp() { }

    public static MensagemWhatsApp Receber(Guid empresaId, string telefone, string? nome,
        string texto, string tipo, string? wamId, DateTime dataHora)
        => new()
        {
            EmpresaId = empresaId, Telefone = telefone, NomeContato = nome,
            Direcao = DirecaoMensagemWhatsApp.Recebida, Texto = texto, Tipo = tipo,
            WamId = wamId, DataHora = dataHora, Lida = false
        };

    public static MensagemWhatsApp Enviar(Guid empresaId, string telefone, string? nome,
        string texto, string? wamId)
        => new()
        {
            EmpresaId = empresaId, Telefone = telefone, NomeContato = nome,
            Direcao = DirecaoMensagemWhatsApp.Enviada, Texto = texto, Tipo = "text",
            WamId = wamId, DataHora = DateTime.UtcNow, Lida = true
        };

    public void MarcarLida() => Lida = true;
}

public enum DirecaoMensagemWhatsApp { Recebida, Enviada }
