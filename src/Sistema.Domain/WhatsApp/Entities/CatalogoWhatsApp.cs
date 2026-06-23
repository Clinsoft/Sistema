using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

public class CatalogoWhatsApp : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public string? WhatsAppBusinessId { get; private set; }  // ID na Meta API
    public string? CatalogId { get; private set; }           // ID do catálogo na Meta
    public ProvedorWhatsApp Provedor { get; private set; }
    public string? ApiToken { get; private set; }            // token criptografado
    public string? WebhookUrl { get; private set; }
    public bool Ativo { get; private set; } = true;
    public DateTime? UltimaSincronizacao { get; private set; }

    private readonly List<ItemCatalogo> _itens = [];
    public IReadOnlyList<ItemCatalogo> Itens => _itens.AsReadOnly();

    private CatalogoWhatsApp() { }

    public static CatalogoWhatsApp Criar(Guid empresaId, string nome,
        ProvedorWhatsApp provedor, string? descricao = null)
        => new() { EmpresaId = empresaId, Nome = nome, Provedor = provedor, Descricao = descricao };

    public void AdicionarItem(Guid produtoId, string descricao, decimal preco,
        string? urlFoto = null, bool disponivel = true)
        => _itens.Add(ItemCatalogo.Criar(Id, produtoId, descricao, preco, urlFoto, disponivel));

    public void RemoverItem(Guid produtoId)
    {
        var item = _itens.FirstOrDefault(i => i.ProdutoId == produtoId);
        if (item != null) _itens.Remove(item);
    }

    public void RegistrarSincronizacao() => UltimaSincronizacao = DateTime.UtcNow;
    public void Desativar() => Ativo = false;
}

public enum ProvedorWhatsApp { MetaCloudApi, ZApi, EvolutionApi }
