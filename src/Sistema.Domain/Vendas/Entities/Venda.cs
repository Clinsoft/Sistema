using Sistema.Domain.Shared.Primitives;
using Sistema.Domain.Vendas.Events;

namespace Sistema.Domain.Vendas.Entities;

public class Venda : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Numero { get; private set; } = null!;
    public Guid? ClienteId { get; private set; }
    public Guid UsuarioId { get; private set; }        // Operador do caixa (dono da sessão)
    public Guid? VendedorId { get; private set; }       // Colaborador que efetuou a venda (comissão/relatórios)
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

    // CPF do consumidor para NFC-e (opcional, informado no PDV)
    public string? CpfCnpjConsumidor { get; private set; }

    // ID da NFC-e gerada ao finalizar (null até emissão ser confirmada pela SEFAZ)
    public Guid? NotaFiscalId { get; private set; }

    private readonly List<ItemVenda> _itens = [];
    public IReadOnlyList<ItemVenda> Itens => _itens.AsReadOnly();

    private readonly List<PagamentoVenda> _pagamentos = [];
    public IReadOnlyList<PagamentoVenda> Pagamentos => _pagamentos.AsReadOnly();

    private Venda() { }

    public static Venda Iniciar(Guid empresaId, Guid usuarioId, Guid localEstoqueId, string numero,
        Guid? clienteId = null, Guid? vendedorId = null)
        => new()
        {
            EmpresaId = empresaId,
            Numero = numero,
            UsuarioId = usuarioId,
            VendedorId = vendedorId,
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

    public void AdicionarPagamento(FormaPagamento forma, decimal valor, int parcelas = 1,
        string? descricao = null, Guid? operadoraCartaoId = null)
    {
        _pagamentos.Add(PagamentoVenda.Criar(Id, forma, Math.Round(valor, 2, MidpointRounding.AwayFromZero),
            parcelas, descricao, operadoraCartaoId));
        TotalPago = _pagamentos.Sum(p => p.Valor);
        Troco = Math.Max(0, TotalPago - Total);
    }

    public void InformarCpfCnpjConsumidor(string cpfOuCnpj) => CpfCnpjConsumidor = cpfOuCnpj.ToUpperInvariant();

    public void VincularNotaFiscal(Guid notaFiscalId) => NotaFiscalId = notaFiscalId;

    public void Finalizar()
    {
        if (!_itens.Any()) throw new InvalidOperationException("Venda sem itens.");
        // Tolerância de arredondamento: o front soma em ponto flutuante (JS) e pode
        // divergir do decimal do backend em até ~1 centavo POR ITEM (vendas por kg).
        var tolerancia = Math.Max(0.005m, _itens.Count * 0.01m);
        if (TotalPago < Total - tolerancia) throw new InvalidOperationException("Pagamento insuficiente.");

        Status = StatusVenda.Finalizada;
        DataHoraFechamento = DateTime.Now;
        RaiseDomainEvent(new VendaFinalizadaEvent(
            Id, EmpresaId, ClienteId, CpfCnpjConsumidor, LocalEstoqueId, _itens.ToList(), Pagamentos.ToList(), Total));
    }

    public void Cancelar(string motivo)
    {
        if (Status == StatusVenda.Cancelada) return;
        Status = StatusVenda.Cancelada;
        Observacao = motivo;
    }

    /// <summary>Marca a venda como já revisada na tela de duplicatas (não é duplicata,
    /// ou já tratada) para não reaparecer na detecção. Marcador discreto na observação.</summary>
    public void MarcarDuplicataRevisada()
    {
        if (Observacao is null || !Observacao.Contains("[dup-ok]"))
            Observacao = string.IsNullOrEmpty(Observacao) ? "[dup-ok]" : $"{Observacao} [dup-ok]";
    }

    private void RecalcularTotais()
    {
        // Usa o TOTAL JÁ ARREDONDADO de cada item (ItemVenda.Total) — mesma base do
        // que aparece na tela, do cupom NFC-e (vProd) e do que o cliente paga. Antes o
        // SubTotal rearredondava com outro critério (AwayFromZero) e dava 1 centavo a mais,
        // gerando "pagamento insuficiente" em vendas por kg.
        TotalDesconto = _itens.Sum(i => i.TotalDesconto);
        SubTotal = _itens.Sum(i => i.Total) + TotalDesconto;   // bruto = líquido dos itens + descontos
        Total = _itens.Sum(i => i.Total) + TotalAcrescimo;     // = soma dos itens + acréscimo
    }
}

public enum StatusVenda { EmAberto, Finalizada, Cancelada }
