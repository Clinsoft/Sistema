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
        decimal precoUnitario, decimal percentualDesconto = 0, decimal? descontoValor = null)
    {
        var item = ItemVenda.Criar(Id, produtoId, descricao, quantidade, precoUnitario, percentualDesconto, descontoValor);
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

    /// <summary>Vincula um cliente cadastrado à venda (ex.: match automático pelo CPF do consumidor).</summary>
    public void VincularCliente(Guid clienteId) => ClienteId = clienteId;

    /// <summary>Resgata cashback como desconto na venda, rateado entre os itens
    /// (mantém a NFC-e válida: aumenta o vDesc dos itens e reduz o total).
    /// Retorna o valor efetivamente aplicado.</summary>
    public decimal ResgatarCashback(decimal valor)
    {
        valor = Math.Round(valor, 2);
        if (valor <= 0 || _itens.Count == 0) return 0m;

        var totalItens = _itens.Sum(i => i.Total);
        if (valor > totalItens) valor = totalItens;   // nunca zera/negativa a venda
        if (valor <= 0) return 0m;

        decimal aplicado = 0;
        for (var k = 0; k < _itens.Count; k++)
        {
            var parcela = k == _itens.Count - 1
                ? valor - aplicado                                  // resíduo de arredondamento no último
                : Math.Round(valor * (_itens[k].Total / totalItens), 2);
            _itens[k].AplicarDescontoAdicional(parcela);
            aplicado += parcela;
        }

        RecalcularTotais();
        return valor;
    }

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
            Id, EmpresaId, ClienteId, CpfCnpjConsumidor, LocalEstoqueId, _itens.ToList(), Pagamentos.ToList(), Total, Numero));
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
