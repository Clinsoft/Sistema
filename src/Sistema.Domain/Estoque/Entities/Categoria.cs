using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class Categoria : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public Guid? CategoriaPaiId { get; private set; }
    public bool Ativo { get; private set; } = true;

    private Categoria() { }

    public static Categoria Criar(Guid empresaId, string nome, Guid? categoriaPaiId = null)
        => new() { EmpresaId = empresaId, Nome = nome, CategoriaPaiId = categoriaPaiId };
}
