using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class TabelaNutricional : Entity
{
    public Guid ProdutoId { get; private set; }
    public string Porcao { get; private set; } = null!;         // ex: "1 cápsula (500mg)"
    public decimal? Calorias { get; private set; }              // kcal
    public decimal? CaloriasGordura { get; private set; }
    public decimal? GordurasTotais { get; private set; }        // g
    public decimal? GordurasSaturadas { get; private set; }
    public decimal? GordurasTrans { get; private set; }
    public decimal? Colesterol { get; private set; }            // mg
    public decimal? Sodio { get; private set; }                 // mg
    public decimal? CarboidratosTotais { get; private set; }    // g
    public decimal? FibrasDieteticas { get; private set; }      // g
    public decimal? Acucares { get; private set; }              // g
    public decimal? Proteinas { get; private set; }             // g
    public decimal? VitaminaA { get; private set; }             // % VD
    public decimal? VitaminaC { get; private set; }
    public decimal? VitaminaD { get; private set; }
    public decimal? VitaminaE { get; private set; }
    public decimal? VitaminaK { get; private set; }
    public decimal? VitaminaB1 { get; private set; }
    public decimal? VitaminaB2 { get; private set; }
    public decimal? VitaminaB3 { get; private set; }
    public decimal? VitaminaB6 { get; private set; }
    public decimal? VitaminaB12 { get; private set; }
    public decimal? AcidoFolico { get; private set; }
    public decimal? Calcio { get; private set; }
    public decimal? Ferro { get; private set; }
    public decimal? Magnesio { get; private set; }
    public decimal? Zinco { get; private set; }
    public decimal? Selenio { get; private set; }
    public string? InformacoesAdicionais { get; private set; }
    public string? Ingredientes { get; private set; }
    public string? Alergenicos { get; private set; }
    public string? ModoConservacao { get; private set; }

    private TabelaNutricional() { }

    public static TabelaNutricional Criar(Guid produtoId, string porcao)
        => new() { ProdutoId = produtoId, Porcao = porcao };

    public void Atualizar(string porcao, decimal? calorias, decimal? proteinas,
        decimal? carboidratos, decimal? gordurasTotais, decimal? sodio,
        string? ingredientes = null, string? alergenicos = null,
        string? modoConservacao = null, string? informacoesAdicionais = null)
    {
        Porcao = porcao;
        Calorias = calorias;
        Proteinas = proteinas;
        CarboidratosTotais = carboidratos;
        GordurasTotais = gordurasTotais;
        Sodio = sodio;
        Ingredientes = ingredientes;
        Alergenicos = alergenicos;
        ModoConservacao = modoConservacao;
        InformacoesAdicionais = informacoesAdicionais;
    }
}
