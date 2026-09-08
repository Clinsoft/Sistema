using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Financeiro.Entities;

/// <summary>Faturamento mensal histórico por loja (importado — anterior ao registro de vendas no sistema).</summary>
public class HistoricoFaturamentoLoja : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public decimal Faturamento { get; private set; }

    private HistoricoFaturamentoLoja() { }

    public static HistoricoFaturamentoLoja Criar(Guid empresaId, Guid localEstoqueId, int ano, int mes, decimal faturamento)
        => new() { EmpresaId = empresaId, LocalEstoqueId = localEstoqueId, Ano = ano, Mes = mes, Faturamento = faturamento };

    public void Atualizar(decimal faturamento) { Faturamento = faturamento; AtualizadoEm = DateTime.UtcNow; }
}
