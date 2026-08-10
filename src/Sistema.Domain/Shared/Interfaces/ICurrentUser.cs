namespace Sistema.Domain.Shared.Interfaces;

/// <summary>Usuário logado no request atual (para auditoria). Implementado na API via HttpContext.</summary>
public interface ICurrentUser
{
    Guid? UsuarioId { get; }
    string? Nome { get; }
    Guid? EmpresaId { get; }
    string? Ip { get; }
}
