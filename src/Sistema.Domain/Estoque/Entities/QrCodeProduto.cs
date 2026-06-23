using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class QrCodeProduto : Entity
{
    public Guid ProdutoId { get; private set; }
    public string Slug { get; private set; } = null!;       // URL amigável: "oleo-de-coco-extra-virgem"
    public string UrlPublica { get; private set; } = null!; // https://loja.com/produto/oleo-de-coco-extra-virgem
    public string QrCodeBase64 { get; private set; } = null!;
    public int TotalScans { get; private set; }
    public DateTime? UltimoScan { get; private set; }

    private QrCodeProduto() { }

    public static QrCodeProduto Criar(Guid produtoId, string slug, string urlBase)
    {
        var url = $"{urlBase.TrimEnd('/')}/produto/{slug}";
        return new QrCodeProduto
        {
            ProdutoId = produtoId,
            Slug = slug,
            UrlPublica = url,
            QrCodeBase64 = string.Empty   // gerado pelo serviço de infraestrutura
        };
    }

    public void RegistrarScan()
    {
        TotalScans++;
        UltimoScan = DateTime.UtcNow;
    }

    public void AtualizarQrCode(string base64) => QrCodeBase64 = base64;
}
