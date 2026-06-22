using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class UnidadeMedida : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Sigla { get; private set; } = null!;   // UN, KG, CX, ML, L, G
    public string Descricao { get; private set; } = null!;
    public bool Ativo { get; private set; } = true;

    private UnidadeMedida() { }

    public static UnidadeMedida Criar(Guid empresaId, string sigla, string descricao)
        => new() { EmpresaId = empresaId, Sigla = sigla.ToUpper(), Descricao = descricao };
}
