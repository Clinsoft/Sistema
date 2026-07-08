using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Marketing.Entities;

/// <summary>
/// Campanha promocional (desconto, leve X pague Y, progressivo, combo, pix, aniversariante).
/// </summary>
public class Promocao : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Tipo { get; private set; } = "Desconto";          // Desconto | LeveXPagueY | DescontoProgressivo | Combo | Pix | Aniversariante
    public string TipoDesconto { get; private set; } = "Percentual"; // Percentual | ValorFixo
    public decimal Desconto { get; private set; }

    public DateTime DataInicio { get; private set; }
    public DateTime? DataFim { get; private set; }

    public string AplicaEm { get; private set; } = "Todos";          // Todos | Categoria | Marca | Produto
    public Guid? ReferenciaId { get; private set; }

    // Regras específicas
    public int QtdeLeve { get; private set; }
    public int QtdePague { get; private set; }
    public decimal ValorMinimoPedido { get; private set; }
    public int LimiteUso { get; private set; }

    public bool ApenasClube { get; private set; }
    public bool Cumulativo { get; private set; }

    public int QtdeUsada { get; private set; }
    public bool Ativa { get; private set; } = true;

    private Promocao() { }

    public static Promocao Criar(Guid empresaId, string nome, string tipo, string tipoDesconto,
        decimal desconto, DateTime dataInicio, DateTime? dataFim,
        string aplicaEm, Guid? referenciaId,
        int qtdeLeve, int qtdePague, decimal valorMinimoPedido, int limiteUso,
        bool apenasClube, bool cumulativo)
        => new()
        {
            EmpresaId = empresaId,
            Nome = nome,
            Tipo = tipo,
            TipoDesconto = tipoDesconto,
            Desconto = desconto,
            DataInicio = dataInicio,
            DataFim = dataFim,
            AplicaEm = aplicaEm,
            ReferenciaId = referenciaId,
            QtdeLeve = qtdeLeve,
            QtdePague = qtdePague,
            ValorMinimoPedido = valorMinimoPedido,
            LimiteUso = limiteUso,
            ApenasClube = apenasClube,
            Cumulativo = cumulativo,
            Ativa = true,
        };

    public void Editar(string nome, string tipo, string tipoDesconto, decimal desconto,
        DateTime dataInicio, DateTime? dataFim, string aplicaEm, Guid? referenciaId,
        int qtdeLeve, int qtdePague, decimal valorMinimoPedido, int limiteUso,
        bool apenasClube, bool cumulativo)
    {
        Nome = nome;
        Tipo = tipo;
        TipoDesconto = tipoDesconto;
        Desconto = desconto;
        DataInicio = dataInicio;
        DataFim = dataFim;
        AplicaEm = aplicaEm;
        ReferenciaId = referenciaId;
        QtdeLeve = qtdeLeve;
        QtdePague = qtdePague;
        ValorMinimoPedido = valorMinimoPedido;
        LimiteUso = limiteUso;
        ApenasClube = apenasClube;
        Cumulativo = cumulativo;
        AtualizadoEm = DateTime.UtcNow;
    }

    public void DefinirAtiva(bool ativa)
    {
        Ativa = ativa;
        AtualizadoEm = DateTime.UtcNow;
    }

    public void RegistrarUso() => QtdeUsada++;

    /// <summary>Status calculado para exibição: Encerrada | Agendada | Ativa.</summary>
    public string StatusCalculado()
    {
        var hoje = DateTime.Today;
        if (!Ativa) return "Encerrada";
        if (DataInicio.Date > hoje) return "Agendada";
        if (DataFim.HasValue && DataFim.Value.Date < hoje) return "Encerrada";
        return "Ativa";
    }
}
