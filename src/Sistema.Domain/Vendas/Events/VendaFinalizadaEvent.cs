using Sistema.Domain.Shared.Primitives;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.Domain.Vendas.Events;

public record VendaFinalizadaEvent(
    Guid VendaId,
    Guid EmpresaId,
    Guid? ClienteId,
    IReadOnlyList<ItemVenda> Itens,
    decimal Total) : IDomainEvent;
