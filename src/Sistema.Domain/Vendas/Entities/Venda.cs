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
        // Tolerância de meio centavo: o front soma valores em ponto flutuante
        // (ex.: 9,03 + 3,62 = 12,6499…) e o total do backend é decimal exato.
        if (TotalPago < Total - 0.005m) throw new InvalidOperationException("Pagamento insuficiente.");

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
        // Soma os itens JÁ ARREDONDADOS por linha (2 casas) — mesmo critério da NFC-e,
        // pra o total da venda bater com o total do cupom fiscal (evita 1 centavo de
        // diferença em vendas por kg com vários itens).
        SubTotal = _itens.Sum(i => Math.Round(i.Quantidade * i.PrecoUnitario, 2, MidpointRounding.AwayFromZero));
        TotalDesconto = _itens.Sum(i => i.TotalDesconto);
        Total = SubTotal - TotalDesconto + TotalAcrescimo;
    }
}

public enum StatusVenda { EmAberto, Finalizada, Cancelada }
