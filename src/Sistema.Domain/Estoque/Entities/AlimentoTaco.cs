using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Base nutricional derivada da Tabela Brasileira de Composição de Alimentos (TACO/UNICAMP)
/// e TBCA/USP. Valores por 100g ou 100ml de alimento.
/// </summary>
public class AlimentoTaco : Entity
{
    public string Nome { get; private set; } = null!;
    public string? NomeCientifico { get; private set; }
    public string GrupoAlimentar { get; private set; } = null!;
    public string Fonte { get; private set; } = "TACO";

    // Energia
    public decimal? CaloriasKcal { get; private set; }
    public decimal? CaloriasKj { get; private set; }

    // Macronutrientes (g)
    public decimal? Umidade { get; private set; }
    public decimal? Carboidratos { get; private set; }
    public decimal? Proteinas { get; private set; }
    public decimal? LipidiosTotais { get; private set; }
    public decimal? FibraAlimentar { get; private set; }
    public decimal? Cinzas { get; private set; }
    public decimal? Colesterol { get; private set; }
    public decimal? AcucaresTotais { get; private set; }

    // Lipídios (g)
    public decimal? GordurasSaturadas { get; private set; }
    public decimal? GordurasMono { get; private set; }
    public decimal? GordurasPoli { get; private set; }
    public decimal? GordurasTrans { get; private set; }

    // Minerais (mg, exceto indicado)
    public decimal? Sodio { get; private set; }
    public decimal? Potassio { get; private set; }
    public decimal? Calcio { get; private set; }
    public decimal? Magnesio { get; private set; }
    public decimal? Manganes { get; private set; }
    public decimal? Fosforo { get; private set; }
    public decimal? Ferro { get; private set; }
    public decimal? Zinco { get; private set; }
    public decimal? Cobre { get; private set; }
    public decimal? Selenio { get; private set; }

    // Vitaminas
    public decimal? VitaminaA { get; private set; }
    public decimal? VitaminaD { get; private set; }
    public decimal? VitaminaE { get; private set; }
    public decimal? VitaminaK { get; private set; }
    public decimal? VitaminaC { get; private set; }
    public decimal? Tiamina { get; private set; }
    public decimal? Riboflavina { get; private set; }
    public decimal? Niacina { get; private set; }
    public decimal? VitaminaB6 { get; private set; }
    public decimal? AcidoFolico { get; private set; }
    public decimal? VitaminaB12 { get; private set; }

    private AlimentoTaco() { }

    public static AlimentoTaco Criar(string nome, string grupoAlimentar, string fonte = "TACO")
        => new() { Nome = nome, GrupoAlimentar = grupoAlimentar, Fonte = fonte };
}
