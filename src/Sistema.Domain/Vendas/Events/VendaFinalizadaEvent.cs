using Sistema.Domain.Shared.Primitives;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.Domain.Vendas.Events;

public record VendaFinalizadaEvent(
    Guid VendaId,
    Guid EmpresaId,
    Guid? ClienteId,
    string? CpfCnpjConsumidor,
    Guid LocalEstoqueId,
    IReadOnlyList<ItemVenda> Itens,
    IReadOnlyList<PagamentoVenda> Pagamentos,
    decimal Total) : IDomainEvent;
