using FluentValidation;
using MediatR;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Commands;

public record IniciarVendaCommand(Guid EmpresaId, Guid UsuarioId, Guid LocalEstoqueId,
    Guid? ClienteId = null, Guid? VendedorId = null)
    : IRequest<Guid>;

public class IniciarVendaHandler(IVendaRepository repo, IUnitOfWork uow)
    : IRequestHandler<IniciarVendaCommand, Guid>
{
    public async Task<Guid> Handle(IniciarVendaCommand cmd, CancellationToken ct)
    {
        var numero = await repo.ProximoNumeroAsync(cmd.EmpresaId, ct);
        var venda = Venda.Iniciar(cmd.EmpresaId, cmd.UsuarioId, cmd.LocalEstoqueId, numero, cmd.ClienteId, cmd.VendedorId);
        await repo.AdicionarAsync(venda, ct);
        await uow.SalvarAsync(ct);
        return venda.Id;
    }
}
