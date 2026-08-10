using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Auditoria.Entities;

/// <summary>Registro de auditoria: quem fez o quê (criou/alterou/excluiu) e quando.</summary>
public class AuditLog : Entity
{
    public Guid? EmpresaId { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public string? UsuarioNome { get; private set; }
    public string Acao { get; private set; } = null!;        // Inserir | Atualizar | Excluir
    public string Entidade { get; private set; } = null!;    // ex.: Lote, Produto, Venda
    public string? EntidadeId { get; private set; }
    public string? Resumo { get; private set; }              // descrição curta do registro
    public string? Alteracoes { get; private set; }          // campos alterados (nas edições)
    public DateTime DataHora { get; private set; }
    public string? Ip { get; private set; }

    private AuditLog() { }

    public static AuditLog Criar(Guid? empresaId, Guid? usuarioId, string? usuarioNome,
        string acao, string entidade, string? entidadeId, string? resumo, string? alteracoes,
        DateTime dataHora, string? ip)
        => new()
        {
            EmpresaId = empresaId, UsuarioId = usuarioId, UsuarioNome = usuarioNome,
            Acao = acao, Entidade = entidade, EntidadeId = entidadeId,
            Resumo = resumo, Alteracoes = alteracoes, DataHora = dataHora, Ip = ip
        };
}
