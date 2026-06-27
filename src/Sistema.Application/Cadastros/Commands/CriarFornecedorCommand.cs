using FluentValidation;
using MediatR;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Cadastros.Commands;

public record CriarFornecedorCommand(
    Guid EmpresaId, string RazaoSocial, string Cnpj,
    string? NomeFantasia = null, string? Email = null,
    string? Telefone = null, string? Contato = null,
    int PrazoPagamentoDias = 30) : IRequest<Guid>;

public class CriarFornecedorValidator : AbstractValidator<CriarFornecedorCommand>
{
    public CriarFornecedorValidator()
    {
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(150);
        // Formato RF 2026: 12 alfanuméricos + 2 dígitos verificadores numéricos
        RuleFor(x => x.Cnpj).NotEmpty().Length(14)
            .Matches(@"^[A-Z0-9]{12}\d{2}$")
            .WithMessage("CNPJ deve ter 14 caracteres (12 alfanuméricos + 2 dígitos verificadores).");
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.PrazoPagamentoDias).GreaterThanOrEqualTo(0);
    }
}

public class CriarFornecedorHandler(IFornecedorRepository repo, IUnitOfWork uow)
    : IRequestHandler<CriarFornecedorCommand, Guid>
{
    public async Task<Guid> Handle(CriarFornecedorCommand cmd, CancellationToken ct)
    {
        if (await repo.ExisteAsync(f => f.EmpresaId == cmd.EmpresaId && f.Cnpj == cmd.Cnpj, ct))
            throw new InvalidOperationException("Já existe fornecedor com este CNPJ.");

        var fornecedor = Fornecedor.Criar(cmd.EmpresaId, cmd.RazaoSocial, cmd.Cnpj,
            cmd.NomeFantasia, cmd.Email, cmd.Telefone);

        await repo.AdicionarAsync(fornecedor, ct);
        await uow.SalvarAsync(ct);
        return fornecedor.Id;
    }
}
