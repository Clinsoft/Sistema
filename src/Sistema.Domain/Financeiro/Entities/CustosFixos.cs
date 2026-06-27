using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Financeiro.Entities;

/// <summary>Custo fixo mensal para cálculo do Ponto de Equilíbrio.</summary>
public class CustoFixo : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public string? Categoria { get; private set; }
    public decimal Valor { get; private set; }
    public bool Ativo { get; private set; } = true;

    private CustoFixo() { }

    public static CustoFixo Criar(Guid empresaId, string descricao, decimal valor, string? categoria = null)
        => new() { EmpresaId = empresaId, Descricao = descricao, Valor = valor, Categoria = categoria };

    public void Atualizar(string descricao, decimal valor, string? categoria)
    {
        Descricao = descricao; Valor = valor; Categoria = categoria;
    }

    public void Desativar() => Ativo = false;
}
