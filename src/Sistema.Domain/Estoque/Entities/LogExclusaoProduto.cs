using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>Registro de auditoria de EXCLUSÃO de produto: guarda o que foi apagado,
/// por quem e quando — para rastrear sumiços de produto.</summary>
public class LogExclusaoProduto : Entity
{
    public Guid    EmpresaId    { get; private set; }
    public Guid    ProdutoId    { get; private set; }
    public string  Codigo       { get; private set; } = null!;
    public string  Descricao    { get; private set; } = null!;
    public decimal PrecoVenda   { get; private set; }
    public Guid?   UsuarioId    { get; private set; }
    public string? UsuarioNome  { get; private set; }
    public string  Origem       { get; private set; } = null!;   // "Exclusão manual" / "Unificação de duplicados"
    public DateTime ExcluidoEm  { get; private set; }

    private LogExclusaoProduto() { }

    public static LogExclusaoProduto Criar(Guid empresaId, Guid produtoId, string codigo,
        string descricao, decimal precoVenda, Guid? usuarioId, string? usuarioNome, string origem)
        => new()
        {
            EmpresaId = empresaId, ProdutoId = produtoId, Codigo = codigo, Descricao = descricao,
            PrecoVenda = precoVenda, UsuarioId = usuarioId, UsuarioNome = usuarioNome,
            Origem = origem, ExcluidoEm = DateTime.UtcNow
        };
}
