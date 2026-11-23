using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class Lote : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public string NumeroLote { get; private set; } = null!;
    public DateTime? DataFabricacao { get; private set; }
    public DateTime? DataValidade { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal CustoUnitario { get; private set; }
    /// <summary>Foto da etiqueta (validade/lote) tirada no registro, para conferência.</summary>
    public string? ImagemUrl { get; private set; }
    /// <summary>Quando a atendente LANÇOU/confirmou a validade deste lote (não muda na venda).
    /// Usado pela conferência da requisição p/ reconhecer lote relançado reaproveitando um
    /// número de lote de compra anterior (aí o CriadoEm fica antigo, mas isto marca o lançamento).</summary>
    public DateTime? ValidadeLancadaEm { get; private set; }

    private Lote() { }

    public static Lote Criar(Guid empresaId, Guid produtoId, Guid localEstoqueId,
        string numeroLote, decimal quantidade, decimal custoUnitario,
        DateTime? dataFabricacao = null, DateTime? dataValidade = null)
        => new()
        {
            EmpresaId = empresaId,
            ProdutoId = produtoId,
            LocalEstoqueId = localEstoqueId,
            NumeroLote = numeroLote,
            Quantidade = quantidade,
            CustoUnitario = custoUnitario,
            DataFabricacao = NormalizarAno(dataFabricacao),
            DataValidade = NormalizarAno(dataValidade)
        };

    // Ano de 2 dígitos (0027) vira 20xx (2027) — evita validade "vencida" por erro de digitação.
    private static DateTime? NormalizarAno(DateTime? d)
    {
        if (d is null || d.Value.Year >= 100) return d;
        return d.Value.AddYears(2000);
    }

    public bool EstaVencido() => DataValidade.HasValue && DataValidade.Value.Date < DateTime.Today;
    public bool VenceEm(int dias) => DataValidade.HasValue && DataValidade.Value.Date <= DateTime.Today.AddDays(dias);
    public void Baixar(decimal quantidade) => Quantidade = Math.Max(0, Quantidade - quantidade);
    /// <summary>Move o lote para outra loja (transferência de entrada lançada na loja errada).</summary>
    public void MoverPara(Guid localEstoqueId) => LocalEstoqueId = localEstoqueId;
    /// <summary>Soma quantidade ao lote (ex.: mesclar com lote de mesmo número ao transferir de loja).</summary>
    public void AdicionarQuantidade(decimal quantidade) => Quantidade = Math.Max(0, Quantidade + quantidade);
    public void AtualizarQuantidade(decimal quantidade) => Quantidade = Math.Max(0, quantidade);
    public void AtualizarValidade(DateTime dataValidade) => DataValidade = NormalizarAno(dataValidade);
    public void DefinirImagem(string url) => ImagemUrl = url;
    /// <summary>Marca que a validade foi lançada/confirmada pela atendente agora.</summary>
    public void RegistrarLancamentoValidade() => ValidadeLancadaEm = DateTime.UtcNow;

    public void Editar(string numeroLote, decimal quantidade, decimal custoUnitario,
        DateTime? dataFabricacao, DateTime? dataValidade)
    {
        NumeroLote = numeroLote;
        Quantidade = Math.Max(0, quantidade);
        CustoUnitario = custoUnitario;
        DataFabricacao = NormalizarAno(dataFabricacao);
        DataValidade = NormalizarAno(dataValidade);
    }
}
