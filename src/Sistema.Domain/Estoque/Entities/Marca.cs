using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class Marca : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public bool Ativo { get; private set; } = true;

    private Marca() { }

    public static Marca Criar(Guid empresaId, string nome)
        => new() { EmpresaId = empresaId, Nome = nome };
}
