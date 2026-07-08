using Sistema.Domain.Shared.Primitives;
using System.Text.Json;

namespace Sistema.Domain.Financeiro.Entities;

public class OperadoraCartao : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? Cor { get; private set; }
    public string? Icone { get; private set; }
    public string? BandeirasJson { get; private set; }
    public decimal TaxaDebito { get; private set; }
    public decimal TaxaCreditoVista { get; private set; }
    public decimal TaxaCreditoParcelado { get; private set; }
    public decimal TaxaPix { get; private set; }
    public decimal TaxaAntecipacao { get; private set; }
    public int PrazoDiasDebito { get; private set; }
    public int PrazoDiasCreditoVista { get; private set; }
    public int PrazoDiasCreditoParcelado { get; private set; }
    public int PrazoDiasPix { get; private set; }
    public string? Observacao { get; private set; }
    public bool Ativo { get; private set; } = true;

    private OperadoraCartao() { }

    public static OperadoraCartao Criar(Guid empresaId, string nome,
        string? cor, string? icone,
        decimal taxaDebito, decimal taxaCreditoVista, decimal taxaCreditoParcelado,
        int prazoDiasDebito, int prazoDiasCreditoVista, int prazoDiasCreditoParcelado,
        List<string>? bandeiras = null,
        decimal taxaPix = 0, int prazoDiasPix = 0, decimal taxaAntecipacao = 0,
        string? observacao = null)
        => new()
        {
            EmpresaId = empresaId,
            Nome = nome,
            Cor = cor,
            Icone = icone,
            BandeirasJson = bandeiras is { Count: > 0 }
                ? JsonSerializer.Serialize(bandeiras) : null,
            TaxaDebito = taxaDebito,
            TaxaCreditoVista = taxaCreditoVista,
            TaxaCreditoParcelado = taxaCreditoParcelado,
            TaxaPix = taxaPix,
            TaxaAntecipacao = taxaAntecipacao,
            PrazoDiasDebito = prazoDiasDebito,
            PrazoDiasCreditoVista = prazoDiasCreditoVista,
            PrazoDiasCreditoParcelado = prazoDiasCreditoParcelado,
            PrazoDiasPix = prazoDiasPix,
            Observacao = observacao,
        };

    public void Atualizar(string nome, string? cor, string? icone,
        decimal taxaDebito, decimal taxaCreditoVista, decimal taxaCreditoParcelado,
        int prazoDiasDebito, int prazoDiasCreditoVista, int prazoDiasCreditoParcelado,
        List<string>? bandeiras = null,
        decimal taxaPix = 0, int prazoDiasPix = 0, decimal taxaAntecipacao = 0,
        string? observacao = null)
    {
        Nome = nome; Cor = cor; Icone = icone;
        BandeirasJson = bandeiras is { Count: > 0 } ? JsonSerializer.Serialize(bandeiras) : null;
        TaxaDebito = taxaDebito; TaxaCreditoVista = taxaCreditoVista;
        TaxaCreditoParcelado = taxaCreditoParcelado; TaxaPix = taxaPix;
        TaxaAntecipacao = taxaAntecipacao; PrazoDiasDebito = prazoDiasDebito;
        PrazoDiasCreditoVista = prazoDiasCreditoVista;
        PrazoDiasCreditoParcelado = prazoDiasCreditoParcelado; PrazoDiasPix = prazoDiasPix;
        Observacao = observacao;
    }

    public void Desativar() => Ativo = false;
}
