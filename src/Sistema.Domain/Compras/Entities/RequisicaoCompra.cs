using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Compras.Entities;

/// <summary>
/// Requisição de compra interna: o atendente pede o que falta (produto + quantidade),
/// sem escolher fornecedor nem preço. O gestor vê agrupado por fornecedor e gera os
/// pedidos de compra.
/// </summary>
public class RequisicaoCompra : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid? LocalEstoqueId { get; private set; }   // loja de quem pediu
    public Guid UsuarioId { get; private set; }          // solicitante
    public StatusRequisicaoCompra Status { get; private set; }
    public string? Observacao { get; private set; }

    private readonly List<ItemRequisicaoCompra> _itens = [];
    public IReadOnlyList<ItemRequisicaoCompra> Itens => _itens.AsReadOnly();

    private RequisicaoCompra() { }

    public static RequisicaoCompra Criar(Guid empresaId, Guid usuarioId, Guid? localEstoqueId, string? observacao)
        => new()
        {
            EmpresaId = empresaId,
            UsuarioId = usuarioId,
            LocalEstoqueId = localEstoqueId,
            Observacao = observacao,
            Status = StatusRequisicaoCompra.Aberta,
        };

    public void AdicionarItem(Guid produtoId, string descricao, decimal quantidade)
        => _itens.Add(ItemRequisicaoCompra.Criar(Id, produtoId, descricao, quantidade));

    public void Processar() { Status = StatusRequisicaoCompra.Processada; AtualizadoEm = DateTime.UtcNow; }
    public void Cancelar()  { Status = StatusRequisicaoCompra.Cancelada;  AtualizadoEm = DateTime.UtcNow; }
}

public class ItemRequisicaoCompra : Entity
{
    public Guid RequisicaoCompraId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Quantidade { get; private set; }

    private ItemRequisicaoCompra() { }

    public static ItemRequisicaoCompra Criar(Guid requisicaoId, Guid produtoId, string descricao, decimal quantidade)
        => new()
        {
            RequisicaoCompraId = requisicaoId,
            ProdutoId = produtoId,
            Descricao = descricao,
            Quantidade = quantidade,
        };
}

public enum StatusRequisicaoCompra { Aberta, Processada, Cancelada }
