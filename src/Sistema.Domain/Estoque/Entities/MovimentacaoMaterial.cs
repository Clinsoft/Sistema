using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Movimentação de estoque de material de consumo. Separada de
/// MovimentacaoEstoque (mercadorias) para que os saldos e o inventário de
/// materiais não se misturem com os dos produtos de venda.
/// </summary>
public class MovimentacaoMaterial : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid MaterialConsumoId { get; private set; }
    public TipoMovimentacaoMaterial Tipo { get; private set; }
    /// <summary>Sempre positiva; o sinal é dado pelo Tipo.</summary>
    public decimal Quantidade { get; private set; }
    public decimal CustoUnitario { get; private set; }
    public string? DocumentoOrigem { get; private set; }   // chave da NF-e, "AJUSTE", "INVENTARIO"…
    public string? Observacao { get; private set; }
    public Guid? UsuarioId { get; private set; }

    private MovimentacaoMaterial() { }

    public static MovimentacaoMaterial Criar(Guid empresaId, Guid materialId,
        TipoMovimentacaoMaterial tipo, decimal quantidade, decimal custoUnitario,
        string? documentoOrigem = null, Guid? usuarioId = null, string? observacao = null)
        => new()
        {
            EmpresaId = empresaId,
            MaterialConsumoId = materialId,
            Tipo = tipo,
            Quantidade = Math.Abs(quantidade),
            CustoUnitario = custoUnitario,
            DocumentoOrigem = documentoOrigem,
            UsuarioId = usuarioId,
            Observacao = observacao,
        };

    /// <summary>Quantidade com sinal: positiva entra, negativa sai.</summary>
    public decimal QuantidadeComSinal => Tipo switch
    {
        TipoMovimentacaoMaterial.Entrada or TipoMovimentacaoMaterial.AjustePositivo => Quantidade,
        _ => -Quantidade,
    };

    public decimal ValorTotal => Math.Round(Quantidade * CustoUnitario, 2);
}

public enum TipoMovimentacaoMaterial
{
    Entrada,            // NF-e ou compra manual
    ConsumoInterno,     // uso no dia a dia
    Producao,           // consumido na produção/embalagem
    Perda,              // quebra, extravio, vencido
    AjustePositivo,     // ajuste/inventário para mais
    AjusteNegativo,     // ajuste/inventário para menos
}
