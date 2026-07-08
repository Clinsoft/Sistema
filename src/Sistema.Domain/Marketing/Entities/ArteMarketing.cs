using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Marketing.Entities;

public class ArteMarketing : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string Nome { get; private set; } = null!;
    public TipoArteMarketing Tipo { get; private set; }
    public FormatoArte Formato { get; private set; }
    public string LayoutJson { get; private set; } = null!;
    public string? ThumbnailBase64 { get; private set; }
    public string? UrlExportada { get; private set; }         // caminho do arquivo gerado
    public StatusArte Status { get; private set; }

    private ArteMarketing() { }

    public static ArteMarketing Criar(Guid empresaId, string nome,
        TipoArteMarketing tipo, FormatoArte formato, string layoutJson, Guid? templateId = null)
        => new()
        {
            EmpresaId = empresaId,
            Nome = nome,
            Tipo = tipo,
            Formato = formato,
            LayoutJson = layoutJson,
            TemplateId = templateId,
            Status = StatusArte.Rascunho
        };

    public void Finalizar(string? urlExportada = null)
    {
        Status = StatusArte.Finalizada;
        UrlExportada = urlExportada;
    }

    public void AtualizarLayout(string layoutJson, string? thumbnail = null)
    {
        LayoutJson = layoutJson;
        if (thumbnail is not null) ThumbnailBase64 = thumbnail;
    }

    public void Renomear(string nome) => Nome = nome;
}

public enum StatusArte { Rascunho, Finalizada }
