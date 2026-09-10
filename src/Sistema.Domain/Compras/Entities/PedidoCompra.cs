using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Compras.Entities;

public class PedidoCompra : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Numero { get; private set; } = null!;
    public Guid? FornecedorId { get; private set; }   // nulo = "a definir" (ex.: pedido de faltantes/observação)
    public Guid UsuarioId { get; private set; }
    public Guid? LocalEstoqueId { get; private set; }   // unidade/loja de entrega do pedido
    public Guid? RequisicaoCompraId { get; private set; }   // requisição de origem (quando gerado dela)
    public StatusPedidoCompra Status { get; private set; }
    public DateTime DataPedido { get; private set; }
    public DateTime? DataPrevisaoEntrega { get; private set; }
    public DateTime? DataRecebimento { get; private set; }
    public decimal Total { get; private set; }
    public string? Observacao { get; private set; }
    public string? AnexoUrl { get; private set; }   // PDF do fornecedor (resposta/disponibilidade)

    private readonly List<ItemPedidoCompra> _itens = [];
    public IReadOnlyList<ItemPedidoCompra> Itens => _itens.AsReadOnly();

    private PedidoCompra() { }

    public static PedidoCompra Criar(Guid empresaId, Guid? fornecedorId, Guid usuarioId, string numero,
        DateTime? previsaoEntrega = null, Guid? localEstoqueId = null, Guid? requisicaoCompraId = null)
        => new()
        {
            EmpresaId = empresaId,
            FornecedorId = fornecedorId,
            UsuarioId = usuarioId,
            LocalEstoqueId = localEstoqueId,
            RequisicaoCompraId = requisicaoCompraId,
            Numero = numero,
            Status = StatusPedidoCompra.Rascunho,
            DataPedido = DateTime.Now,
            DataPrevisaoEntrega = previsaoEntrega
        };

    /// <summary>Define/ajusta a unidade (loja) de entrega do pedido.</summary>
    public void DefinirLocalEstoque(Guid? localEstoqueId) => LocalEstoqueId = localEstoqueId;

    /// <summary>Define o fornecedor (ex.: pedido de faltantes que estava "a definir").</summary>
    public void DefinirFornecedor(Guid? fornecedorId) => FornecedorId = fornecedorId;

    public void AdicionarItem(Guid produtoId, string descricao, decimal quantidade, decimal precoUnitario)
    {
        _itens.Add(ItemPedidoCompra.Criar(Id, produtoId, descricao, quantidade, precoUnitario));
        Total = _itens.Sum(i => i.Total);
    }

    /// <summary>Remove os itens informados (ex.: faltantes que foram para outro fornecedor) e recalcula o total.</summary>
    public void RemoverItens(IEnumerable<Guid> itemIds)
    {
        var set = itemIds.ToHashSet();
        _itens.RemoveAll(i => set.Contains(i.Id));
        Total = _itens.Sum(i => i.Total);
    }

    public void DefinirAnexo(string? url) => AnexoUrl = url;

    public void Enviar() => Status = StatusPedidoCompra.Enviado;

    public string? NotaFiscalRecebimento { get; private set; }   // NF-e que recebeu a OC (nº/chave)

    public void Receber()
    {
        Status = StatusPedidoCompra.Recebido;
        DataRecebimento = DateTime.Now;
    }

    /// <summary>Recebe a OC registrando a NF-e (nº ou chave) que a atendeu.</summary>
    public void ReceberComNota(string? notaRef)
    {
        Receber();
        NotaFiscalRecebimento = notaRef;
    }

    public void Cancelar() => Status = StatusPedidoCompra.Cancelado;
}

public enum StatusPedidoCompra { Rascunho, Enviado, Recebido, Cancelado }
