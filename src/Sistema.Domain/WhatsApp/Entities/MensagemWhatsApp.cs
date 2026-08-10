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
    public Guid?  LocalEstoqueId { get; private set; }          // loja/unidade dona da conversa (número)
    public string Telefone    { get; private set; } = null!;   // wa_id (só dígitos)
    public string? NomeContato { get; private set; }           // nome do perfil do cliente
    public DirecaoMensagemWhatsApp Direcao { get; private set; }
    public string Texto       { get; private set; } = null!;
    public string Tipo        { get; private set; } = "text";  // text/image/audio/document/video/sticker
    public string? WamId      { get; private set; }
    public DateTime DataHora  { get; private set; }
    public bool   Lida        { get; private set; }

    // Mídia (foto/documento/áudio) — arquivo baixado/enviado servido em /uploads/whatsapp.
    public string? MidiaUrl   { get; private set; }
    public string? MidiaMime  { get; private set; }
    public string? MidiaNome  { get; private set; }   // nome original (documentos)

    // Status de entrega das mensagens ENVIADAS (nulo para recebidas).
    public StatusEntregaWhatsApp? StatusEntrega { get; private set; }

    private MensagemWhatsApp() { }

    public static MensagemWhatsApp Receber(Guid empresaId, string telefone, string? nome,
        string texto, string tipo, string? wamId, DateTime dataHora, Guid? localEstoqueId = null)
        => new()
        {
            EmpresaId = empresaId, LocalEstoqueId = localEstoqueId, Telefone = telefone, NomeContato = nome,
            Direcao = DirecaoMensagemWhatsApp.Recebida, Texto = texto, Tipo = tipo,
            WamId = wamId, DataHora = dataHora, Lida = false
        };

    public static MensagemWhatsApp Enviar(Guid empresaId, string telefone, string? nome,
        string texto, string? wamId, string tipo = "text", Guid? localEstoqueId = null)
        => new()
        {
            EmpresaId = empresaId, LocalEstoqueId = localEstoqueId, Telefone = telefone, NomeContato = nome,
            Direcao = DirecaoMensagemWhatsApp.Enviada, Texto = texto, Tipo = tipo,
            WamId = wamId, DataHora = DateTime.UtcNow, Lida = true,
            StatusEntrega = StatusEntregaWhatsApp.Enviada
        };

    public void DefinirMidia(string url, string? mime, string? nome)
    {
        MidiaUrl = url; MidiaMime = mime; MidiaNome = nome;
    }

    public void MarcarLida() => Lida = true;

    public void AtualizarStatusEntrega(StatusEntregaWhatsApp status)
    {
        // Não regride o status (entregue → lido, mas nunca lido → entregue).
        if (StatusEntrega is null || status > StatusEntrega) StatusEntrega = status;
    }
}

public enum StatusEntregaWhatsApp { Enviada = 1, Entregue = 2, Lida = 3, Falhou = 4 }

public enum DirecaoMensagemWhatsApp { Recebida, Enviada }
