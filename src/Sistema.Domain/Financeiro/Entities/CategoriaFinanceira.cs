using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Financeiro.Entities;

public class CategoriaFinanceira : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public TipoCategoria Tipo { get; private set; }
    public string? Cor { get; private set; }
    public bool Ativo { get; private set; } = true;

    private CategoriaFinanceira() { }

    public static CategoriaFinanceira Criar(Guid empresaId, string nome, TipoCategoria tipo, string? cor = null)
        => new() { EmpresaId = empresaId, Nome = nome, Tipo = tipo, Cor = cor };

    public void Atualizar(string nome, string? cor) { Nome = nome; Cor = cor; }
    public void Desativar() => Ativo = false;
}

public enum TipoCategoria { Receita, Despesa }
