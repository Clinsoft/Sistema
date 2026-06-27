using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class LocalEstoque : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public bool Principal { get; private set; }
    public bool Ativo { get; private set; } = true;

    private LocalEstoque() { }

    public static LocalEstoque Criar(Guid empresaId, string nome, bool principal = false, string? descricao = null)
        => new() { EmpresaId = empresaId, Nome = nome, Principal = principal, Descricao = descricao };

    public void Editar(string nome, bool principal, string? descricao)
    {
        Nome = nome;
        Principal = principal;
        Descricao = descricao;
    }
}
