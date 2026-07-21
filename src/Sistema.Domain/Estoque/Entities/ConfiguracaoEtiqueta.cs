using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

/// <summary>
/// Configuração de um template de etiqueta por empresa (ex.: "ecogranel").
/// O layout é personalizado no editor de etiquetas e guardado como JSON, de
/// forma que o mesmo padrão valha para todos os computadores/usuários da loja.
/// </summary>
public class ConfiguracaoEtiqueta : Entity
{
    public Guid EmpresaId { get; private set; }

    /// <summary>Identificador do template (ex.: "ecogranel", "pote9x9").</summary>
    public string Template { get; private set; } = null!;

    /// <summary>Configuração serializada (cores, textos, escalas, marca d'água…).</summary>
    public string ConfigJson { get; private set; } = "{}";

    // AtualizadoEm é herdado de Entity — não redeclarar aqui (a duplicação causava
    // "Ambiguous match found" por reflexão ao salvar). A coluna é NOT NULL, então
    // preenchemos sempre no Criar e no Atualizar.

    private ConfiguracaoEtiqueta() { }

    public static ConfiguracaoEtiqueta Criar(Guid empresaId, string template, string configJson)
        => new()
        {
            EmpresaId = empresaId,
            Template = template,
            ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson,
            AtualizadoEm = DateTime.UtcNow,
        };

    public void Atualizar(string configJson)
    {
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        AtualizadoEm = DateTime.UtcNow;
    }
}
