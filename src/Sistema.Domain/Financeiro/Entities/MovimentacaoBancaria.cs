using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Financeiro.Entities;

/// <summary>Movimentação efetiva na conta bancária (débito/crédito).</summary>
public class MovimentacaoBancaria : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ContaBancariaId { get; private set; }
    public Guid? LancamentoId { get; private set; }
    public Guid? CategoriaId { get; private set; }
    public TipoMovimentacaoBancaria Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public DateTime DataMovimentacao { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal SaldoApos { get; private set; }

    private MovimentacaoBancaria() { }

    public static MovimentacaoBancaria Criar(Guid empresaId, Guid contaBancariaId,
        TipoMovimentacaoBancaria tipo, decimal valor, string descricao,
        decimal saldoApos, Guid? lancamentoId = null, Guid? categoriaId = null)
        => new()
        {
            EmpresaId = empresaId, ContaBancariaId = contaBancariaId,
            Tipo = tipo, Valor = valor, Descricao = descricao,
            DataMovimentacao = DateTime.Now, SaldoApos = saldoApos,
            LancamentoId = lancamentoId, CategoriaId = categoriaId
        };
}

public enum TipoMovimentacaoBancaria { Credito, Debito, Transferencia }
