using FluentValidation;
using MediatR;
using CrediarioEntity = Sistema.Domain.Crediario.Entities.Crediario;
using Sistema.Domain.Crediario.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Crediario.Commands;

public record AbrirCrediarioCommand(
    Guid EmpresaId, Guid ClienteId, Guid UsuarioId,
    decimal ValorTotal, decimal ValorEntrada,
    int NumeroParcelas, decimal TaxaJurosMensal,
    Guid? VendaId = null, DateTime? DataPrimeiraParcela = null,
    int? DiaVencimento = null, string? Observacao = null) : IRequest<AbrirCrediarioResult>;

public record AbrirCrediarioResult(Guid Id, string Numero, int NumeroParcelas, decimal ValorParcela);

public class AbrirCrediarioValidator : AbstractValidator<AbrirCrediarioCommand>
{
    public AbrirCrediarioValidator()
    {
        RuleFor(x => x.ValorTotal).GreaterThan(0);
        RuleFor(x => x.ValorEntrada).GreaterThanOrEqualTo(0)
            .LessThan(x => x.ValorTotal).WithMessage("Entrada não pode ser maior ou igual ao valor total.");
        RuleFor(x => x.NumeroParcelas).InclusiveBetween(1, 60);
        RuleFor(x => x.TaxaJurosMensal).GreaterThanOrEqualTo(0);
    }
}

public class AbrirCrediarioHandler(ICrediarioRepository repo, IUnitOfWork uow)
    : IRequestHandler<AbrirCrediarioCommand, AbrirCrediarioResult>
{
    public async Task<AbrirCrediarioResult> Handle(AbrirCrediarioCommand cmd, CancellationToken ct)
    {
        var numero = await repo.ProximoNumeroAsync(cmd.EmpresaId, ct);

        var crediario = CrediarioEntity.Criar(
            cmd.EmpresaId, cmd.ClienteId, cmd.UsuarioId,
            numero, cmd.ValorTotal, cmd.ValorEntrada,
            cmd.NumeroParcelas, cmd.TaxaJurosMensal,
            DateTime.Today, cmd.VendaId,
            cmd.DataPrimeiraParcela, cmd.DiaVencimento, cmd.Observacao);

        await repo.AdicionarAsync(crediario, ct);
        await uow.SalvarAsync(ct);

        var valorParcela = crediario.Parcelas.FirstOrDefault()?.Valor ?? 0;
        return new AbrirCrediarioResult(crediario.Id, numero, cmd.NumeroParcelas, valorParcela);
    }
}
