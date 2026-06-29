using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class AlertaValidade : Entity
{
    public Guid    EmpresaId    { get; private set; }
    public Guid    ProdutoId    { get; private set; }
    public Guid?   LoteId       { get; private set; }
    public DateTime DataValidade { get; private set; }

    /// <summary>Amarelo | Vermelho | Urgente | Vencido</summary>
    public string  Nivel        { get; private set; } = null!;
    public bool    PromoGerada  { get; private set; }
    public Guid?   ArteId       { get; private set; }
    public DateTime ProcessadoEm { get; private set; }

    private AlertaValidade() { }

    public static AlertaValidade Criar(Guid empresaId, Guid produtoId, Guid? loteId,
        DateTime dataValidade, string nivel)
        => new()
        {
            EmpresaId    = empresaId,
            ProdutoId    = produtoId,
            LoteId       = loteId,
            DataValidade = dataValidade,
            Nivel        = nivel,
            ProcessadoEm = DateTime.UtcNow,
        };

    public void MarcarPromoGerada(Guid arteId)
    {
        PromoGerada = true;
        ArteId      = arteId;
    }
}
