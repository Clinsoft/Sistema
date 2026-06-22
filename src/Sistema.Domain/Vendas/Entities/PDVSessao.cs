using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Vendas.Entities;

public class PDVSessao : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public DateTime Abertura { get; private set; }
    public DateTime? Fechamento { get; private set; }
    public decimal SaldoAbertura { get; private set; }
    public decimal SaldoFechamento { get; private set; }
    public decimal TotalVendas { get; private set; }
    public decimal TotalSuprimentos { get; private set; }
    public decimal TotalSangrias { get; private set; }
    public StatusSessao Status { get; private set; }
    public string? ObservacaoFechamento { get; private set; }

    private PDVSessao() { }

    public static PDVSessao Abrir(Guid empresaId, Guid usuarioId, Guid localEstoqueId, decimal saldoAbertura)
        => new()
        {
            EmpresaId = empresaId,
            UsuarioId = usuarioId,
            LocalEstoqueId = localEstoqueId,
            SaldoAbertura = saldoAbertura,
            Abertura = DateTime.Now,
            Status = StatusSessao.Aberta
        };

    public void AdicionarVenda(decimal valor) => TotalVendas += valor;
    public void AdicionarSuprimento(decimal valor) => TotalSuprimentos += valor;
    public void AdicionarSangria(decimal valor) => TotalSangrias += valor;

    public void Fechar(decimal saldoFechamento, string? observacao = null)
    {
        Fechamento = DateTime.Now;
        SaldoFechamento = saldoFechamento;
        Status = StatusSessao.Fechada;
        ObservacaoFechamento = observacao;
    }
}

public enum StatusSessao { Aberta, Fechada }
