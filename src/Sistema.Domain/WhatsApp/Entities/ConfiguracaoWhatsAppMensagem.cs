using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

/// <summary>
/// Credenciais da Meta WhatsApp Cloud API por empresa.
/// Obtidas em: Meta for Developers → WhatsApp → API Setup.
/// </summary>
public class ConfiguracaoWhatsAppMensagem : Entity
{
    public Guid   EmpresaId           { get; private set; }

    // Credenciais Meta Cloud API
    public string? PhoneNumberId      { get; private set; }  // ex: 123456789012345
    public string? AccessToken        { get; private set; }  // token do System User (permanente)
    public string? BusinessAccountId  { get; private set; }  // WABA ID
    public string? WebhookVerifyToken { get; private set; }  // token criado pelo lojista para verificar webhook
    public string? AppId              { get; private set; }  // ID do App no Meta

    // Catálogo WhatsApp Business
    public string? CatalogId          { get; private set; }  // ID do catálogo na Meta (Commerce)
    public string? NumeroWhatsApp     { get; private set; }  // número usado no link wa.me

    // Comportamento
    public bool Ativo                 { get; private set; } = false;

    // Regras de disparo automático
    public bool   EnviarAniversario   { get; private set; } = true;
    public bool   EnviarPromocoes     { get; private set; } = true;
    public bool   EnviarNovidades     { get; private set; } = false;
    public int    HoraDisparo         { get; private set; } = 8;   // hora do dia (0-23)

    private ConfiguracaoWhatsAppMensagem() { }

    public static ConfiguracaoWhatsAppMensagem Criar(Guid empresaId)
        => new() { EmpresaId = empresaId };

    public void Atualizar(string? phoneNumberId, string? accessToken,
        string? businessAccountId, string? webhookVerifyToken, string? appId,
        bool ativo, bool enviarAniversario, bool enviarPromocoes, bool enviarNovidades,
        int horaDisparo)
    {
        PhoneNumberId      = phoneNumberId;
        AccessToken        = accessToken;
        BusinessAccountId  = businessAccountId;
        WebhookVerifyToken = webhookVerifyToken;
        AppId              = appId;
        Ativo              = ativo;
        EnviarAniversario  = enviarAniversario;
        EnviarPromocoes    = enviarPromocoes;
        EnviarNovidades    = enviarNovidades;
        HoraDisparo        = Math.Clamp(horaDisparo, 0, 23);
    }

    /// <summary>Atualiza apenas os campos do catálogo (feed) — NÃO toca no token/credenciais
    /// de mensagens, que são gerenciadas no outro bloco.</summary>
    public void AtualizarCatalogo(string? catalogId, string? numeroWhatsApp)
    {
        CatalogId      = catalogId;
        NumeroWhatsApp = numeroWhatsApp;
    }
}
