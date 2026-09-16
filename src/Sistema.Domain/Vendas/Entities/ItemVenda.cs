using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Vendas.Entities;

public class ItemVenda : Entity
{
    public Guid VendaId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal PercentualDesconto { get; private set; }
    public decimal TotalDesconto { get; private set; }
    public decimal Total { get; private set; }

    private ItemVenda() { }

    /// <summary>Acrescenta um desconto ao item (ex.: rateio de resgate de cashback),
    /// reduzindo o Total e recalculando o percentual. Nunca deixa o Total negativo.</summary>
    public void AplicarDescontoAdicional(decimal valor)
    {
        valor = Math.Round(valor, 2);
        if (valor <= 0) return;
        if (valor > Total) valor = Total;   // trava de segurança
        TotalDesconto = Math.Round(TotalDesconto + valor, 2);
        Total = Math.Round(Total - valor, 2);
        var bruto = PrecoUnitario * Quantidade;
        PercentualDesconto = bruto > 0 ? Math.Round(TotalDesconto / bruto * 100, 4) : 0;
    }

    /// <summary>
    /// Cria o item. O desconto pode vir como PORCENTAGEM (percentualDesconto) ou, de
    /// preferência, como VALOR EM REAIS (descontoValor) — este último evita divergência
    /// de arredondamento entre a tela (soma em ponto flutuante do JS) e o decimal do
    /// backend: o valor bruto do item é arredondado a centavos e o desconto é subtraído
    /// exatamente, de modo que o Total bate ao centavo com o que o cliente paga.
    /// </summary>
    public static ItemVenda Criar(Guid vendaId, Guid produtoId, string descricao,
        decimal quantidade, decimal precoUnitario, decimal percentualDesconto = 0,
        decimal? descontoValor = null)
    {
        var bruto = Math.Round(precoUnitario * quantidade, 2);   // mesma base do que aparece na tela e na NFC-e
        var totalDesconto = descontoValor is { } dv
            ? Math.Round(Math.Clamp(dv, 0m, bruto), 2)            // desconto em reais (autoritativo)
            : Math.Round(bruto * percentualDesconto / 100, 2);   // compat.: desconto em %
        return new ItemVenda
        {
            VendaId = vendaId,
            ProdutoId = produtoId,
            Descricao = descricao,
            Quantidade = quantidade,
            PrecoUnitario = precoUnitario,
            PercentualDesconto = bruto > 0 ? Math.Round(totalDesconto / bruto * 100, 4) : 0,
            TotalDesconto = totalDesconto,
            Total = Math.Round(bruto - totalDesconto, 2)
        };
    }
}
