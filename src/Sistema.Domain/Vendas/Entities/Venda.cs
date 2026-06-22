using Sistema.Domain.Shared.Primitives;
using Sistema.Domain.Vendas.Events;

namespace Sistema.Domain.Vendas.Entities;

public class Venda : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Numero { get; private set; } = null!;
    public Guid? ClienteId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public StatusVenda Status { get; private set; }
    public DateTime DataHora { get; private set; }
    public DateTime? DataHoraFechamento { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TotalDesconto { get; private set; }
    public decimal TotalAcrescimo { get; private set; }
    public decimal Total { get; private set; }
    public decimal TotalPago { get; private set; }
    public decimal Troco { get; private set; }

    public string? Observacao { get; private set; }

    private readonly List<ItemVenda> _itens = [];
    public IReadOnlyList<ItemVenda> Itens => _itens.AsReadOnly();

    private readonly List<PagamentoVenda> _pagamentos = [];
    public IReadOnlyList<PagamentoVenda> Pagamentos => _pagamentos.AsReadOnly();

    private Venda() { }

    public static Venda Iniciar(Guid empresaId, Guid usuarioId, Guid localEstoqueId, string numero, Guid? clienteId = null)
        => new()
        {
            EmpresaId = empresaId,
            Numero = numero,
            UsuarioId = usuarioId,
            LocalEstoqueId = localEstoqueId,
            ClienteId = clienteId,
            Status = StatusVenda.EmAberto,
            DataHora = DateTime.Now
        };

    public void AdicionarItem(Guid produtoId, string descricao, decimal quantidade,
        decimal precoUnitario, decimal desconto = 0)
    {
        var item = ItemVenda.Criar(Id, produtoId, descricao, quantidade, precoUnitario, desconto);
        _itens.Add(item);
        RecalcularTotais();
    }

    public void RemoverItem(Guid itemId)
    {
        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        _itens.Remove(item);
        RecalcularTotais();
    }

    public void AdicionarPagamento(FormaPagamento forma, decimal valor, int parcelas = 1, string? descricao = null)
    {
        _pagamentos.Add(PagamentoVenda.Criar(Id, forma, valor, parcelas, descricao));
        TotalPago = _pagamentos.Sum(p => p.Valor);
        Troco = Math.Max(0, TotalPago - Total);
    }

    public void Finalizar()
    {
        if (!_itens.Any()) throw new InvalidOperationException("Venda sem itens.");
        if (TotalPago < Total) throw new InvalidOperationException("Pagamento insuficiente.");

        Status = StatusVenda.Finalizada;
        DataHoraFechamento = DateTime.Now;
        RaiseDomainEvent(new VendaFinalizadaEvent(Id, EmpresaId, ClienteId, _itens.ToList(), Total));
    }

    public void Cancelar(string motivo)
    {
        if (Status == StatusVenda.Cancelada) return;
        Status = StatusVenda.Cancelada;
        Observacao = motivo;
    }

    private void RecalcularTotais()
    {
        SubTotal = _itens.Sum(i => i.Quantidade * i.PrecoUnitario);
        TotalDesconto = _itens.Sum(i => i.TotalDesconto);
        Total = SubTotal - TotalDesconto + TotalAcrescimo;
    }
}

public enum StatusVenda { EmAberto, Finalizada, Cancelada }
