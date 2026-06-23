using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Marketing.Entities;

public class TemplateMarketing : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public TipoArteMarketing Tipo { get; private set; }
    public FormatoArte Formato { get; private set; }
    public string LayoutJson { get; private set; } = null!;    // JSON Fabric.js serializado
    public string? ThumbnailBase64 { get; private set; }
    public bool EhTemplate { get; private set; } = false;      // true = template de fábrica
    public bool Ativo { get; private set; } = true;

    private TemplateMarketing() { }

    public static TemplateMarketing Criar(Guid empresaId, string nome,
        TipoArteMarketing tipo, FormatoArte formato, string layoutJson, bool ehTemplate = false)
        => new()
        {
            EmpresaId = empresaId,
            Nome = nome,
            Tipo = tipo,
            Formato = formato,
            LayoutJson = layoutJson,
            EhTemplate = ehTemplate
        };

    public void AtualizarLayout(string layoutJson, string? thumbnail = null)
    {
        LayoutJson = layoutJson;
        ThumbnailBase64 = thumbnail;
    }
}

public enum TipoArteMarketing
{
    Promocao,
    LancamentoProduto,
    Aniversario,
    DataComemorativa,
    ClubePromocoes,
    CardapioSemanal,
    Generico
}

public enum FormatoArte
{
    FeedQuadrado,    // 1080x1080
    StoryVertical,   // 1080x1920
    BannerHorizontal // 1200x628
}
