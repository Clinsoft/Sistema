namespace Sistema.Domain.Assinaturas;

/// <summary>
/// Fonte ÚNICA da regra de negócio dos planos: quais recursos cada plano libera e os limites
/// (usuários/lojas). Tudo que decide "o plano X pode Y?" passa por aqui — não espalhar regra.
/// </summary>
public static class PlanoCatalogo
{
    public const int Ilimitado = int.MaxValue;

    private static readonly Recurso[] MicroRec =
    [
        Recurso.Pdv, Recurso.Fiscal,
    ];

    private static readonly Recurso[] EssencialRec =
    [
        .. MicroRec,
        Recurso.Estoque, Recurso.Validade, Recurso.Etiquetas,
    ];

    private static readonly Recurso[] ProfissionalRec =
    [
        .. EssencialRec,
        Recurso.Financeiro, Recurso.Whatsapp, Recurso.MarketingIa,
        Recurso.Fidelidade, Recurso.Recebiveis,
    ];

    private static readonly Recurso[] RedeRec =
    [
        .. ProfissionalRec,
        Recurso.MultiLoja, Recurso.DrePorLoja, Recurso.Premiacao,
        Recurso.Cotacoes, Recurso.Concorrencia,
    ];

    /// <summary>Recursos liberados por um plano.</summary>
    public static IReadOnlyCollection<Recurso> Recursos(PlanoAssinatura plano) => plano switch
    {
        PlanoAssinatura.Micro => MicroRec,
        PlanoAssinatura.Essencial => EssencialRec,
        PlanoAssinatura.Profissional => ProfissionalRec,
        PlanoAssinatura.Rede => RedeRec,
        _ => MicroRec,
    };

    public static bool Inclui(PlanoAssinatura plano, Recurso recurso)
        => Recursos(plano).Contains(recurso);

    /// <summary>Máximo de usuários do plano (Ilimitado = sem limite prático).</summary>
    public static int MaxUsuarios(PlanoAssinatura plano) => plano switch
    {
        PlanoAssinatura.Micro => 1,
        PlanoAssinatura.Essencial => 3,
        _ => Ilimitado,
    };

    /// <summary>Máximo de lojas do plano. Rede é definido pelas lojas contratadas.</summary>
    public static int MaxLojas(PlanoAssinatura plano, int lojasContratadas = 1) => plano switch
    {
        PlanoAssinatura.Rede => Math.Max(1, lojasContratadas),
        _ => 1,
    };

    public static string Nome(PlanoAssinatura plano) => plano switch
    {
        PlanoAssinatura.Micro => "Micro",
        PlanoAssinatura.Essencial => "Essencial",
        PlanoAssinatura.Profissional => "Profissional",
        PlanoAssinatura.Rede => "Rede",
        _ => plano.ToString(),
    };
}
