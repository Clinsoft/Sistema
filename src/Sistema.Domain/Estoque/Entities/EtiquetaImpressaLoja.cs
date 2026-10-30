using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Registra quando a etiqueta de um produto foi (re)impressa EM CADA LOJA.
/// Permite controlar a reimpressão por unidade: a etiqueta fica pendente para a
/// loja que ainda não reimprimiu desde a última alteração de preço do produto.
/// </summary>
public class EtiquetaImpressaLoja : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public DateTime ImpressaEm { get; private set; }

    private EtiquetaImpressaLoja() { }

    public static EtiquetaImpressaLoja Criar(Guid empresaId, Guid produtoId, Guid localEstoqueId)
        => new()
        {
            EmpresaId = empresaId,
            ProdutoId = produtoId,
            LocalEstoqueId = localEstoqueId,
            ImpressaEm = DateTime.UtcNow,
        };

    public void RegistrarImpressao() { ImpressaEm = DateTime.UtcNow; AtualizadoEm = DateTime.UtcNow; }
}
