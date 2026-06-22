using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Compras.Entities;

public class PedidoCompra : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Numero { get; private set; } = null!;
    public Guid FornecedorId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public StatusPedidoCompra Status { get; private set; }
    public DateTime DataPedido { get; private set; }
    public DateTime? DataPrevisaoEntrega { get; private set; }
    public DateTime? DataRecebimento { get; private set; }
    public decimal Total { get; private set; }
    public string? Observacao { get; private set; }

    private readonly List<ItemPedidoCompra> _itens = [];
    public IReadOnlyList<ItemPedidoCompra> Itens => _itens.AsReadOnly();

    private PedidoCompra() { }

    public static PedidoCompra Criar(Guid empresaId, Guid fornecedorId, Guid usuarioId, string numero,
        DateTime? previsaoEntrega = null)
        => new()
        {
            EmpresaId = empresaId,
            FornecedorId = fornecedorId,
            UsuarioId = usuarioId,
            Numero = numero,
            Status = StatusPedidoCompra.Rascunho,
            DataPedido = DateTime.Now,
            DataPrevisaoEntrega = previsaoEntrega
        };

    public void AdicionarItem(Guid produtoId, string descricao, decimal quantidade, decimal precoUnitario)
    {
        _itens.Add(ItemPedidoCompra.Criar(Id, produtoId, descricao, quantidade, precoUnitario));
        Total = _itens.Sum(i => i.Total);
    }

    public void Enviar() => Status = StatusPedidoCompra.Enviado;

    public void Receber()
    {
        Status = StatusPedidoCompra.Recebido;
        DataRecebimento = DateTime.Now;
    }

    public void Cancelar() => Status = StatusPedidoCompra.Cancelado;
}

public enum StatusPedidoCompra { Rascunho, Enviado, Recebido, Cancelado }
