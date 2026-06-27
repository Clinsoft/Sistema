using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Financeiro.Entities;

public class ContaBancaria : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? Banco { get; private set; }
    public string? Agencia { get; private set; }
    public string? NumeroConta { get; private set; }
    public TipoConta Tipo { get; private set; }
    public decimal SaldoInicial { get; private set; }
    public decimal SaldoAtual { get; private set; }
    public bool Ativo { get; private set; } = true;
    public bool ContaPrincipal { get; private set; }

    private ContaBancaria() { }

    public static ContaBancaria Criar(Guid empresaId, string nome, TipoConta tipo,
        decimal saldoInicial = 0, string? banco = null, string? agencia = null,
        string? numeroConta = null, bool contaPrincipal = false)
        => new()
        {
            EmpresaId = empresaId, Nome = nome, Tipo = tipo,
            SaldoInicial = saldoInicial, SaldoAtual = saldoInicial,
            Banco = banco, Agencia = agencia, NumeroConta = numeroConta,
            ContaPrincipal = contaPrincipal
        };

    public void Creditar(decimal valor)
    {
        if (valor <= 0) throw new InvalidOperationException("Valor deve ser positivo.");
        SaldoAtual += valor;
    }

    public void Debitar(decimal valor)
    {
        if (valor <= 0) throw new InvalidOperationException("Valor deve ser positivo.");
        SaldoAtual -= valor;
    }

    public void AjustarSaldo(decimal novoSaldo) => SaldoAtual = novoSaldo;
    public void Desativar() => Ativo = false;
}

public enum TipoConta { ContaCorrente, ContaPoupanca, Caixa, Investimento, Cartao }
