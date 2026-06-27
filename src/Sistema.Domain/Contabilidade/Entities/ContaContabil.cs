using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Contabilidade.Entities;

/// <summary>Plano de contas CFC — hierárquico.</summary>
public class ContaContabil : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Codigo { get; private set; } = null!;        // ex: 1.1.01.001
    public string Nome { get; private set; } = null!;
    public NaturezaConta Natureza { get; private set; }
    public TipoConta Tipo { get; private set; }                // Sintética | Analítica
    public Guid? ContaPaiId { get; private set; }
    public int Nivel { get; private set; }
    public bool AceitaLancamento { get; private set; }         // só contas analíticas
    public bool Ativo { get; private set; } = true;

    private ContaContabil() { }

    public static ContaContabil Criar(Guid empresaId, string codigo, string nome,
        NaturezaConta natureza, TipoConta tipo, int nivel,
        Guid? contaPaiId = null, bool aceitaLancamento = false)
        => new()
        {
            EmpresaId = empresaId, Codigo = codigo, Nome = nome,
            Natureza = natureza, Tipo = tipo, Nivel = nivel,
            ContaPaiId = contaPaiId,
            AceitaLancamento = tipo == TipoConta.Analitica || aceitaLancamento
        };

    public void Renomear(string nome) => Nome = nome;
    public void Desativar() => Ativo = false;
}

public enum NaturezaConta { Ativo, Passivo, PatrimonioLiquido, Receita, Despesa, Custo }
public enum TipoConta { Sintetica, Analitica }
