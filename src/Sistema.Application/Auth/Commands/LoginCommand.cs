using FluentValidation;
using MediatR;
using Sistema.Domain.Auth;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Auth.Commands;

public record LoginCommand(string Email, string Senha, Guid? EmpresaId = null) : IRequest<LoginResult>;

public record LoginResult(string Token, string Nome, string Perfil, DateTime Expiracao);

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Senha).NotEmpty();
    }
}

public class LoginHandler(IUsuarioRepository repo, IJwtTokenService jwt, IUnitOfWork uow)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var usuario = await repo.ObterPorEmailAsync(cmd.EmpresaId, cmd.Email, ct)
            ?? throw new UnauthorizedAccessException("E-mail ou senha inválidos.");

        if (!BCrypt.Net.BCrypt.Verify(cmd.Senha, usuario.SenhaHash))
            throw new UnauthorizedAccessException("E-mail ou senha inválidos.");

        usuario.RegistrarAcesso();
        repo.Atualizar(usuario);
        await uow.SalvarAsync(ct);

        var token = jwt.GerarToken(usuario);
        return new LoginResult(token, usuario.Nome, usuario.Perfil, jwt.Expiracao);
    }
}
