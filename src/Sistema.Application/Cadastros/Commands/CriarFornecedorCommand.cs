using FluentValidation;
using MediatR;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Cadastros.Commands;

public record CriarFornecedorCommand(
    Guid EmpresaId, string RazaoSocial, string? Cnpj = null,
    string? NomeFantasia = null, string? Email = null,
    string? Telefone = null, string? Celular = null, string? Contato = null,
    string? Tipos = null, int PrazoPagamentoDias = 30,
    string? Logradouro = null, string? Numero = null, string? Complemento = null,
    string? Bairro = null, string? Cidade = null, string? Uf = null,
    string? Cep = null, string? InscricaoEstadual = null) : IRequest<Guid>;

public class CriarFornecedorValidator : AbstractValidator<CriarFornecedorCommand>
{
    public CriarFornecedorValidator()
    {
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(150);
        // CPF = 11 dígitos; CNPJ alfanumérico = 14 chars (12 alfanum + 2 dígitos)
        RuleFor(x => x.Cnpj)
            .Must(cnpj => cnpj is null || cnpj.Length == 11 || cnpj.Length == 14)
            .WithMessage("CPF deve ter 11 dígitos ou CNPJ 14 caracteres.")
            .When(x => x.Cnpj is not null);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.PrazoPagamentoDias).GreaterThanOrEqualTo(0);
    }
}

public class CriarFornecedorHandler(IFornecedorRepository repo, IUnitOfWork uow)
    : IRequestHandler<CriarFornecedorCommand, Guid>
{
    public async Task<Guid> Handle(CriarFornecedorCommand cmd, CancellationToken ct)
    {
        if (cmd.Cnpj is not null &&
            await repo.ExisteAsync(f => f.EmpresaId == cmd.EmpresaId && f.Cnpj == cmd.Cnpj, ct))
            throw new InvalidOperationException("Já existe fornecedor com este CPF/CNPJ.");

        var fornecedor = Fornecedor.Criar(cmd.EmpresaId, cmd.RazaoSocial, cmd.Cnpj,
            cmd.NomeFantasia, cmd.Email, cmd.Telefone);

        // Aplica endereço, contato, celular, tipos e prazo informados na criação
        fornecedor.Editar(cmd.RazaoSocial, cmd.NomeFantasia, cmd.Email, cmd.Telefone,
            cmd.Contato, cmd.PrazoPagamentoDias, cmd.Logradouro, cmd.Numero, cmd.Complemento,
            cmd.Bairro, cmd.Cidade, cmd.Uf, cmd.Cep, cmd.InscricaoEstadual, null,
            cmd.Celular, cmd.Tipos);

        await repo.AdicionarAsync(fornecedor, ct);
        await uow.SalvarAsync(ct);
        return fornecedor.Id;
    }
}
