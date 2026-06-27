using FluentValidation;
using MediatR;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Auth.Commands;

public record CriarUsuarioCommand(
    Guid EmpresaId, string Nome, string Email, string Senha, string Perfil) : IRequest<Guid>;

public class CriarUsuarioValidator : AbstractValidator<CriarUsuarioCommand>
{
    private static readonly string[] PerfisValidos = ["Administrador", "Vendedor", "Financeiro", "Contador"];

    public CriarUsuarioValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Perfil).Must(p => PerfisValidos.Contains(p))
            .WithMessage("Perfil inválido. Use: Administrador, Vendedor, Financeiro ou Contador.");
    }
}

public class CriarUsuarioHandler(IUsuarioRepository repo, IUnitOfWork uow)
    : IRequestHandler<CriarUsuarioCommand, Guid>
{
    public async Task<Guid> Handle(CriarUsuarioCommand cmd, CancellationToken ct)
    {
        if (await repo.ExisteAsync(u => u.EmpresaId == cmd.EmpresaId && u.Email == cmd.Email, ct))
            throw new InvalidOperationException("Já existe usuário com este e-mail.");

        var hash = BCrypt.Net.BCrypt.HashPassword(cmd.Senha);
        var usuario = Usuario.Criar(cmd.EmpresaId, cmd.Nome, cmd.Email, hash, cmd.Perfil);

        await repo.AdicionarAsync(usuario, ct);
        await uow.SalvarAsync(ct);
        return usuario.Id;
    }
}
