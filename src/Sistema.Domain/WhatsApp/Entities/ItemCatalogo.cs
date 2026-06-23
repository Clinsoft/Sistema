using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

public class ItemCatalogo : Entity
{
    public Guid CatalogoId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Preco { get; private set; }
    public string? UrlFoto { get; private set; }
    public bool Disponivel { get; private set; } = true;
    public string? IdExterno { get; private set; }      // ID no catálogo Meta/Z-API

    private ItemCatalogo() { }

    public static ItemCatalogo Criar(Guid catalogoId, Guid produtoId, string descricao,
        decimal preco, string? urlFoto = null, bool disponivel = true)
        => new()
        {
            CatalogoId = catalogoId,
            ProdutoId = produtoId,
            Descricao = descricao,
            Preco = preco,
            UrlFoto = urlFoto,
            Disponivel = disponivel
        };

    public void AtualizarPreco(decimal novoPreco) => Preco = novoPreco;
    public void Indisponibilizar() => Disponivel = false;
    public void DefinirIdExterno(string id) => IdExterno = id;
}
