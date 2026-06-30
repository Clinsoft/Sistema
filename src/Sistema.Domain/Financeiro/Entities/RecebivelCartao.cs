using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Financeiro.Entities;

public class RecebivelCartao : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid OperadoraCartaoId { get; private set; }
    public Guid? VendaId { get; private set; }
    public string FormaPagamento { get; private set; } = null!;
    public int Parcelas { get; private set; }
    public decimal ValorBruto { get; private set; }
    public decimal Taxa { get; private set; }
    public decimal ValorLiquido { get; private set; }
    public DateTime DataTransacao { get; private set; }
    public DateTime DataPrevistaRepasse { get; private set; }
    public DateTime? DataRepasse { get; private set; }
    public DateTime? DataAntecipacao { get; private set; }
    public decimal? TaxaAntecipacaoAplicada { get; private set; }
    public StatusRecebivelCartao Status { get; private set; }
    public string? NsuTid { get; private set; }

    public OperadoraCartao? Operadora { get; private set; }

    private RecebivelCartao() { }

    public static RecebivelCartao Criar(Guid empresaId, Guid operadoraId, Guid? vendaId,
        string formaPagamento, int parcelas, decimal valorBruto, decimal taxa,
        DateTime dataTransacao, DateTime dataPrevistaRepasse, string? nsuTid = null)
    {
        var liquido = valorBruto - (valorBruto * taxa / 100m);
        return new()
        {
            EmpresaId = empresaId,
            OperadoraCartaoId = operadoraId,
            VendaId = vendaId,
            FormaPagamento = formaPagamento,
            Parcelas = parcelas,
            ValorBruto = valorBruto,
            Taxa = taxa,
            ValorLiquido = Math.Round(liquido, 2),
            DataTransacao = dataTransacao,
            DataPrevistaRepasse = dataPrevistaRepasse,
            Status = StatusRecebivelCartao.Pendente,
            NsuTid = nsuTid,
        };
    }

    public void MarcarRecebido()
    {
        Status = StatusRecebivelCartao.Recebido;
        DataRepasse = DateTime.UtcNow;
    }

    public void MarcarAntecipado(decimal taxaAntecipacao)
    {
        TaxaAntecipacaoAplicada = taxaAntecipacao;
        ValorLiquido = Math.Round(ValorLiquido - (ValorLiquido * taxaAntecipacao / 100m), 2);
        Status = StatusRecebivelCartao.Antecipado;
        DataAntecipacao = DateTime.UtcNow;
        DataRepasse = DateTime.UtcNow;
    }
}

public enum StatusRecebivelCartao
{
    Pendente,
    Recebido,
    Antecipado,
    Cancelado
}
