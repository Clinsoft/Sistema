using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Vendas.Entities;

/// <summary>Meta de venda de um mês dentro do planejamento anual (valor definido/editado pelo lojista).</summary>
public class MetaVendaMensal : Entity
{
    public Guid EmpresaId { get; private set; }
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public decimal Valor { get; private set; }

    private MetaVendaMensal() { }

    public static MetaVendaMensal Criar(Guid empresaId, int ano, int mes, decimal valor)
        => new() { EmpresaId = empresaId, Ano = ano, Mes = mes, Valor = valor };

    public void DefinirValor(decimal valor) => Valor = valor;
}
