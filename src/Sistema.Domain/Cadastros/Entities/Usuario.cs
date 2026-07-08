using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Cadastros.Entities;

public class Usuario : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string SenhaHash { get; private set; } = null!;
    public string Perfil { get; private set; } = null!; // Administrador, Vendedor, Financeiro, Contador
    public bool Ativo { get; private set; } = true;
    public DateTime? UltimoAcesso { get; private set; }

    private Usuario() { }

    public static Usuario Criar(Guid empresaId, string nome, string email, string senhaHash, string perfil)
        => new() { EmpresaId = empresaId, Nome = nome, Email = email, SenhaHash = senhaHash, Perfil = perfil };

    public void RegistrarAcesso() => UltimoAcesso = DateTime.UtcNow;
    public void Desativar() => Ativo = false;
    public void Reativar() => Ativo = true;
    public void AlterarPerfil(string perfil) => Perfil = perfil;
    public void AlterarNome(string nome) => Nome = nome;
    public void AlterarSenha(string novoHash) => SenhaHash = novoHash;
}
