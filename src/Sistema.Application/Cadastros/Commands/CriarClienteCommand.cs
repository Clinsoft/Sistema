using FluentValidation;
using MediatR;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Cadastros.Commands;

public record CriarClienteCommand(
    Guid EmpresaId, string Nome, string TipoPessoa,
    string? CpfCnpj = null, string? Email = null,
    string? Telefone = null, string? Celular = null,
    DateTime? DataNascimento = null, string? Classificacao = null,
    decimal LimiteCredito = 0,
    string? Logradouro = null, string? Numero = null, string? Complemento = null,
    string? Bairro = null, string? Cidade = null, string? Uf = null, string? Cep = null)
    : IRequest<Guid>;

public class CriarClienteValidator : AbstractValidator<CriarClienteCommand>
{
    public CriarClienteValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TipoPessoa).Must(t => t is "Fisica" or "Juridica")
            .WithMessage("TipoPessoa deve ser 'Fisica' ou 'Juridica'.");
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.LimiteCredito).GreaterThanOrEqualTo(0);
    }
}

public class CriarClienteHandler(IClienteRepository repo, IUnitOfWork uow)
    : IRequestHandler<CriarClienteCommand, Guid>
{
    public async Task<Guid> Handle(CriarClienteCommand cmd, CancellationToken ct)
    {
        if (cmd.CpfCnpj is not null &&
            await repo.ExisteAsync(c => c.EmpresaId == cmd.EmpresaId && c.CpfCnpj == cmd.CpfCnpj, ct))
            throw new InvalidOperationException("Já existe cliente com este CPF/CNPJ.");

        var tipo = Enum.Parse<TipoPessoa>(cmd.TipoPessoa);
        var cliente = Cliente.Criar(cmd.EmpresaId, cmd.Nome, tipo,
            cmd.CpfCnpj, cmd.Email, cmd.Telefone, cmd.Celular, cmd.DataNascimento);

        // Aplica endereço, limite de crédito e classificação informados na criação
        cliente.AtualizarDados(cmd.Nome, cmd.Email, cmd.Telefone, cmd.Celular,
            cmd.Logradouro, cmd.Numero, cmd.Complemento, cmd.Bairro,
            cmd.Cidade, cmd.Uf, cmd.Cep, cmd.LimiteCredito, cmd.Classificacao);

        await repo.AdicionarAsync(cliente, ct);
        await uow.SalvarAsync(ct);
        return cliente.Id;
    }
}
