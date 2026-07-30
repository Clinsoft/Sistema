using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Cadastros.Entities;

/// <summary>
/// Colaborador / funcionário da empresa. O acesso ao sistema (login) é OPCIONAL:
/// quem só recebe salário fica sem e-mail/senha/perfil; quem precisa operar o
/// sistema recebe acesso via <see cref="ConcederAcesso"/>. A tabela continua se
/// chamando "Usuarios" por compatibilidade.
/// </summary>
public class Usuario : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;

    // Dados de funcionário (RH / folha)
    public string? Cpf { get; private set; }
    public string? Telefone { get; private set; }
    public string? Cargo { get; private set; }
    public decimal? Salario { get; private set; }
    public DateTime? DataAdmissao { get; private set; }
    public string? Observacao { get; private set; }

    // Acesso ao sistema (opcional)
    public string? Email { get; private set; }
    public string? SenhaHash { get; private set; }
    public string? Perfil { get; private set; }   // Administrador, Vendedor, Financeiro, Contador
    public bool TemAcesso => !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(SenhaHash);

    public bool Ativo { get; private set; } = true;
    public DateTime? UltimoAcesso { get; private set; }
    public Guid? LocalEstoqueId { get; private set; }   // Unidade/loja do colaborador

    public void DefinirUnidade(Guid? localEstoqueId) => LocalEstoqueId = localEstoqueId;

    private Usuario() { }

    /// <summary>Cria um colaborador COM acesso ao sistema (fluxo antigo de usuário).</summary>
    public static Usuario Criar(Guid empresaId, string nome, string email, string senhaHash, string perfil)
        => new()
        {
            EmpresaId = empresaId, Nome = nome,
            Email = email, SenhaHash = senhaHash, Perfil = perfil,
        };

    /// <summary>Cria um colaborador SEM login (só dados de funcionário).</summary>
    public static Usuario CriarColaborador(Guid empresaId, string nome,
        string? cpf = null, string? telefone = null, string? cargo = null,
        decimal? salario = null, DateTime? dataAdmissao = null, string? observacao = null)
        => new()
        {
            EmpresaId = empresaId, Nome = nome,
            Cpf = cpf, Telefone = telefone, Cargo = cargo,
            Salario = salario, DataAdmissao = dataAdmissao, Observacao = observacao,
        };

    public void EditarDadosColaborador(string nome, string? cpf, string? telefone,
        string? cargo, decimal? salario, DateTime? dataAdmissao, string? observacao)
    {
        Nome = nome; Cpf = cpf; Telefone = telefone; Cargo = cargo;
        Salario = salario; DataAdmissao = dataAdmissao; Observacao = observacao;
    }

    /// <summary>Concede (ou atualiza) o acesso ao sistema para este colaborador.</summary>
    public void ConcederAcesso(string email, string senhaHash, string perfil)
    {
        Email = email; SenhaHash = senhaHash; Perfil = perfil;
    }

    /// <summary>Remove o acesso ao sistema, mantendo o cadastro do colaborador.</summary>
    public void RevogarAcesso()
    {
        Email = null; SenhaHash = null; Perfil = null;
    }

    public void RegistrarAcesso() => UltimoAcesso = DateTime.UtcNow;
    public void Desativar() => Ativo = false;
    public void Reativar() => Ativo = true;
    public void AlterarPerfil(string perfil) => Perfil = perfil;
    public void AlterarNome(string nome) => Nome = nome;
    public void AlterarSenha(string novoHash) => SenhaHash = novoHash;
}
