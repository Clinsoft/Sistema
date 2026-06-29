using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class ConfiguracaoValidade : Entity
{
    public Guid EmpresaId { get; private set; }

    // Limiares em dias
    public int DiasAlertaAmarelo { get; private set; } = 60;
    public int DiasAlertaVermelho { get; private set; } = 30;
    public int DiasAlertaUrgente  { get; private set; } = 15;

    // Promoção automática
    public bool   PromoAutomatica       { get; private set; } = true;
    public bool   ExigeAprovacao        { get; private set; } = false;
    public decimal DescontoAutoPercent  { get; private set; } = 30m;

    // Comportamento
    public bool BloqueioVendaVencido { get; private set; } = false;

    // Filtro de categorias (JSON array de Guid) — null = todas
    public string? CategoriasJson { get; private set; }

    private ConfiguracaoValidade() { }

    public static ConfiguracaoValidade Padrao(Guid empresaId)
        => new() { EmpresaId = empresaId };

    public void Atualizar(int diasAmarelo, int diasVermelho, int diasUrgente,
        bool promoAuto, bool exigeAprovacao, decimal descontoPercent,
        bool bloqueioVencido, string? categoriasJson)
    {
        DiasAlertaAmarelo    = diasAmarelo;
        DiasAlertaVermelho   = diasVermelho;
        DiasAlertaUrgente    = diasUrgente;
        PromoAutomatica      = promoAuto;
        ExigeAprovacao       = exigeAprovacao;
        DescontoAutoPercent  = descontoPercent;
        BloqueioVendaVencido = bloqueioVencido;
        CategoriasJson       = categoriasJson;
    }
}
