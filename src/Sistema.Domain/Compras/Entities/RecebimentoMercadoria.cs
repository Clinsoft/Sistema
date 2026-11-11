using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Compras.Entities;

/// <summary>
/// Confirmação de que a mercadoria CHEGOU FISICAMENTE na loja. É necessária porque
/// alguns fornecedores (ex.: Vida em Grãos) mandam o XML da NF já com lote e validade;
/// ao importar, o sistema cria entrada + lote de uma vez, mesmo a mercadoria ainda não
/// tendo chegado. Enquanto não houver este recebimento, o produto fica "Em trânsito".
/// </summary>
public class RecebimentoMercadoria : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public Guid ProdutoId { get; private set; }
    /// <summary>Chave da NF que estava em trânsito quando o recebimento foi confirmado (rastro).</summary>
    public string? ChaveNota { get; private set; }
    public DateTime RecebidoEm { get; private set; }
    public Guid? RecebidoPorUsuarioId { get; private set; }

    private RecebimentoMercadoria() { }

    public static RecebimentoMercadoria Criar(Guid empresaId, Guid localEstoqueId, Guid produtoId,
        string? chaveNota, Guid? recebidoPorUsuarioId)
        => new()
        {
            EmpresaId = empresaId,
            LocalEstoqueId = localEstoqueId,
            ProdutoId = produtoId,
            ChaveNota = chaveNota,
            RecebidoEm = DateTime.UtcNow,
            RecebidoPorUsuarioId = recebidoPorUsuarioId,
        };

    public void Reconfirmar(string? chaveNota, Guid? usuarioId)
    {
        ChaveNota = chaveNota;
        RecebidoEm = DateTime.UtcNow;
        RecebidoPorUsuarioId = usuarioId;
        AtualizadoEm = DateTime.UtcNow;
    }
}
