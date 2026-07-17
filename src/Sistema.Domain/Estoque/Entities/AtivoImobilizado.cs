using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Bem do ativo imobilizado (balança, PDV, impressora, móvel, veículo…).
/// Diferente de mercadoria e de material de consumo: não é vendido nem consumido
/// — é um bem da empresa, controlado por unidade, valor e depreciação.
/// </summary>
public class AtivoImobilizado : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string Descricao { get; private set; } = null!;
    public CategoriaAtivo Categoria { get; private set; } = CategoriaAtivo.Equipamento;

    public Guid? FornecedorPrincipalId { get; private set; }
    public string? NotaFiscal { get; private set; }        // chave/número da NF-e de compra
    public string? NumeroSerie { get; private set; }
    public string? Localizacao { get; private set; }

    public DateTime DataAquisicao { get; private set; }
    public decimal ValorAquisicao { get; private set; }
    public decimal Quantidade { get; private set; } = 1;

    /// <summary>Vida útil em meses para depreciação linear (0 = não deprecia).</summary>
    public int VidaUtilMeses { get; private set; }
    /// <summary>Valor residual ao fim da vida útil.</summary>
    public decimal ValorResidual { get; private set; }

    public DateTime? DataBaixa { get; private set; }
    public string? MotivoBaixa { get; private set; }
    public string? Observacao { get; private set; }
    public bool Ativo { get; private set; } = true;

    private AtivoImobilizado() { }

    public static AtivoImobilizado Criar(Guid empresaId, string codigo, string descricao,
        decimal valorAquisicao, DateTime dataAquisicao, CategoriaAtivo categoria = CategoriaAtivo.Equipamento,
        Guid? fornecedorId = null, decimal quantidade = 1)
        => new()
        {
            EmpresaId = empresaId,
            Codigo = codigo,
            Descricao = descricao,
            ValorAquisicao = valorAquisicao,
            DataAquisicao = dataAquisicao,
            Categoria = categoria,
            FornecedorPrincipalId = fornecedorId,
            Quantidade = quantidade <= 0 ? 1 : quantidade,
        };

    public void Editar(string descricao, CategoriaAtivo categoria, Guid? fornecedorId,
        decimal valorAquisicao, DateTime dataAquisicao, decimal quantidade,
        int vidaUtilMeses, decimal valorResidual, string? numeroSerie,
        string? localizacao, string? observacao, bool ativo)
    {
        Descricao = descricao;
        Categoria = categoria;
        FornecedorPrincipalId = fornecedorId;
        ValorAquisicao = valorAquisicao;
        DataAquisicao = dataAquisicao;
        Quantidade = quantidade <= 0 ? 1 : quantidade;
        VidaUtilMeses = vidaUtilMeses < 0 ? 0 : vidaUtilMeses;
        ValorResidual = valorResidual < 0 ? 0 : valorResidual;
        NumeroSerie = numeroSerie;
        Localizacao = localizacao;
        Observacao = observacao;
        Ativo = ativo;
    }

    public void DefinirOrigemNota(string? notaFiscal) => NotaFiscal = notaFiscal;

    /// <summary>Baixa do bem (venda, descarte, perda). Mantém o histórico.</summary>
    public void Baixar(DateTime data, string motivo)
    {
        if (DataBaixa.HasValue) throw new InvalidOperationException("Bem já baixado.");
        DataBaixa = data;
        MotivoBaixa = motivo;
        Ativo = false;
    }

    /// <summary>Meses decorridos desde a aquisição (limitado à vida útil).</summary>
    public int MesesDepreciados(DateTime? referencia = null)
    {
        if (VidaUtilMeses <= 0) return 0;
        var ate = DataBaixa ?? referencia ?? DateTime.Today;
        var meses = ((ate.Year - DataAquisicao.Year) * 12) + ate.Month - DataAquisicao.Month;
        return Math.Clamp(meses, 0, VidaUtilMeses);
    }

    /// <summary>Depreciação mensal linear: (valor − residual) ÷ vida útil.</summary>
    public decimal DepreciacaoMensal =>
        VidaUtilMeses > 0 ? Math.Round((ValorAquisicao - ValorResidual) / VidaUtilMeses, 2) : 0m;

    public decimal DepreciacaoAcumulada(DateTime? referencia = null) =>
        Math.Round(DepreciacaoMensal * MesesDepreciados(referencia), 2);

    /// <summary>Valor contábil atual: aquisição − depreciação acumulada.</summary>
    public decimal ValorContabil(DateTime? referencia = null) =>
        Math.Round(ValorAquisicao - DepreciacaoAcumulada(referencia), 2);
}

/// <summary>Categoria do bem, para agrupar no relatório de imobilizado.</summary>
public enum CategoriaAtivo
{
    Equipamento,        // balança, PDV, impressora, computador
    Movel,              // gôndola, prateleira, mesa
    Veiculo,
    Imovel,
    Software,
    Outro,
}
