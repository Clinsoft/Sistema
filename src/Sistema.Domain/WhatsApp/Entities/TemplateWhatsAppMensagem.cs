using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

/// <summary>
/// Mapeia um template aprovado no Meta Business Manager para um tipo de disparo.
/// O template precisa ser criado e aprovado pela Meta antes de ser usado.
/// </summary>
public class TemplateWhatsAppMensagem : Entity
{
    public Guid   EmpresaId   { get; private set; }

    /// <summary>Nome exato do template no Meta (ex: "aniversario_cliente").</summary>
    public string NomeMeta    { get; private set; } = null!;

    public string Idioma      { get; private set; } = "pt_BR";
    public TipoDisparoWhatsApp TipoDisparo { get; private set; }

    /// <summary>
    /// Mapeamento das variáveis do template para campos do sistema.
    /// JSON: [{"posicao":1,"campo":"nome_cliente"},{"posicao":2,"campo":"desconto"}]
    /// Campos disponíveis: nome_cliente, primeiro_nome, telefone, data_aniversario,
    /// desconto, produto_nome, produto_preco, produto_preco_promo, data_validade, link_catalogo
    /// </summary>
    public string? VariaveisJson { get; private set; }

    /// <summary>Exemplo do texto gerado (para preview).</summary>
    public string? ExemploTexto  { get; private set; }

    public bool Ativo { get; private set; } = true;

    private TemplateWhatsAppMensagem() { }

    public static TemplateWhatsAppMensagem Criar(Guid empresaId, string nomeMeta,
        TipoDisparoWhatsApp tipoDisparo, string idioma = "pt_BR",
        string? variaveisJson = null, string? exemploTexto = null)
        => new()
        {
            EmpresaId     = empresaId,
            NomeMeta      = nomeMeta,
            TipoDisparo   = tipoDisparo,
            Idioma        = idioma,
            VariaveisJson = variaveisJson,
            ExemploTexto  = exemploTexto,
        };

    public void Atualizar(string nomeMeta, string idioma,
        string? variaveisJson, string? exemploTexto)
    {
        NomeMeta      = nomeMeta;
        Idioma        = idioma;
        VariaveisJson = variaveisJson;
        ExemploTexto  = exemploTexto;
    }

    public void Ativar()    => Ativo = true;
    public void Desativar() => Ativo = false;
}

public enum TipoDisparoWhatsApp
{
    Aniversario,
    Promocao,
    Novidade,
    BemVindo,
    LembreteCobranca,
    Personalizado,
}
