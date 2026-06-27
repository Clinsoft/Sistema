using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Contabilidade.Entities;

/// <summary>Lançamento contábil a débito e crédito (partidas dobradas).</summary>
public class LancamentoContabil : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Numero { get; private set; } = null!;        // numeração sequencial
    public DateTime DataLancamento { get; private set; }
    public DateTime DataCompetencia { get; private set; }
    public string Historico { get; private set; } = null!;
    public string? DocumentoOrigem { get; private set; }
    public TipoLancamentoContabil Tipo { get; private set; }
    public bool Estornado { get; private set; }
    public Guid? LancamentoEstornoId { get; private set; }

    private readonly List<PartidaContabil> _partidas = [];
    public IReadOnlyList<PartidaContabil> Partidas => _partidas.AsReadOnly();

    private LancamentoContabil() { }

    public static LancamentoContabil Criar(Guid empresaId, string numero,
        DateTime dataCompetencia, string historico,
        TipoLancamentoContabil tipo = TipoLancamentoContabil.Normal,
        string? documentoOrigem = null)
        => new()
        {
            EmpresaId = empresaId, Numero = numero, Historico = historico,
            DataLancamento = DateTime.Now, DataCompetencia = dataCompetencia,
            Tipo = tipo, DocumentoOrigem = documentoOrigem
        };

    public void AdicionarDebito(Guid contaId, decimal valor, string? complemento = null)
        => _partidas.Add(PartidaContabil.Criar(Id, contaId, NatureDebCred.Debito, valor, complemento));

    public void AdicionarCredito(Guid contaId, decimal valor, string? complemento = null)
        => _partidas.Add(PartidaContabil.Criar(Id, contaId, NatureDebCred.Credito, valor, complemento));

    public bool EstaBalanceado()
    {
        var debitos = _partidas.Where(p => p.Natureza == NatureDebCred.Debito).Sum(p => p.Valor);
        var creditos = _partidas.Where(p => p.Natureza == NatureDebCred.Credito).Sum(p => p.Valor);
        return debitos == creditos;
    }

    public void Estornar(Guid lancamentoEstornoId)
    {
        Estornado = true;
        LancamentoEstornoId = lancamentoEstornoId;
    }
}

public enum TipoLancamentoContabil { Normal, Estorno, Abertura, Encerramento, Automatico }

public class PartidaContabil : Entity
{
    public Guid LancamentoContabilId { get; private set; }
    public Guid ContaContabilId { get; private set; }
    public NatureDebCred Natureza { get; private set; }
    public decimal Valor { get; private set; }
    public string? Complemento { get; private set; }

    private PartidaContabil() { }

    public static PartidaContabil Criar(Guid lancamentoId, Guid contaId,
        NatureDebCred natureza, decimal valor, string? complemento = null)
        => new()
        {
            LancamentoContabilId = lancamentoId, ContaContabilId = contaId,
            Natureza = natureza, Valor = valor, Complemento = complemento
        };
}

public enum NatureDebCred { Debito, Credito }
