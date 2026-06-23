using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

public class PedidoWhatsApp : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid? ClienteId { get; private set; }
    public string TelefoneCliente { get; private set; } = null!;
    public string NomeCliente { get; private set; } = null!;
    public string Numero { get; private set; } = null!;
    public StatusPedidoWhatsApp Status { get; private set; }
    public decimal Total { get; private set; }
    public string? Observacao { get; private set; }
    public string? EnderecoEntrega { get; private set; }
    public TipoEntregaWhatsApp TipoEntrega { get; private set; }
    public StatusPagamentoWhatsApp StatusPagamento { get; private set; }
    public string? PixCopiaCola { get; private set; }
    public DateTime? DataPagamento { get; private set; }

    private readonly List<ItemPedidoWhatsApp> _itens = [];
    public IReadOnlyList<ItemPedidoWhatsApp> Itens => _itens.AsReadOnly();

    private PedidoWhatsApp() { }

    public static PedidoWhatsApp Criar(Guid empresaId, string telefone, string nomeCliente,
        string numero, TipoEntregaWhatsApp tipoEntrega, Guid? clienteId = null)
        => new()
        {
            EmpresaId = empresaId,
            TelefoneCliente = telefone,
            NomeCliente = nomeCliente,
            Numero = numero,
            TipoEntrega = tipoEntrega,
            ClienteId = clienteId,
            Status = StatusPedidoWhatsApp.Novo,
            StatusPagamento = StatusPagamentoWhatsApp.Pendente
        };

    public void AdicionarItem(Guid produtoId, string descricao, decimal quantidade, decimal precoUnitario)
    {
        _itens.Add(ItemPedidoWhatsApp.Criar(Id, produtoId, descricao, quantidade, precoUnitario));
        Total = _itens.Sum(i => i.Total);
    }

    public void AvancarStatus(StatusPedidoWhatsApp novoStatus) => Status = novoStatus;
    public void RegistrarPagamento() { StatusPagamento = StatusPagamentoWhatsApp.Pago; DataPagamento = DateTime.UtcNow; }
    public void DefinirPix(string copiaCola) => PixCopiaCola = copiaCola;
    public void DefinirEndereco(string endereco) => EnderecoEntrega = endereco;
}

public enum StatusPedidoWhatsApp { Novo, Confirmado, EmSeparacao, ProntoParaRetirada, Enviado, Entregue, Cancelado }
public enum TipoEntregaWhatsApp { Retirada, Entrega }
public enum StatusPagamentoWhatsApp { Pendente, Pago, Cancelado }
