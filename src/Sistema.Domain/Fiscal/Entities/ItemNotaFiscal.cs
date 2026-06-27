using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Fiscal.Entities;

public class ItemNotaFiscal : Entity
{
    public Guid NotaFiscalId { get; private set; }
    public Guid? ProdutoId { get; private set; }
    public int NumeroItem { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string Descricao { get; private set; } = null!;
    public string? Ncm { get; private set; }
    public string? Cest { get; private set; }
    public string Cfop { get; private set; } = null!;
    public string UnidadeMedida { get; private set; } = null!;
    /// <summary>
    /// true = pesável (KG, LT…): quantidade com 3 casas decimais no XML (<qCom>).
    /// false = não pesável (UN, CX…): quantidade inteira.
    /// Controla a precisão do campo qCom/qTrib conforme NT SEFAZ.
    /// </summary>
    public bool Pesavel { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public decimal ValorDesconto { get; private set; }
    public decimal ValorTotal { get; private set; }

    // ICMS — permanece até 2032 com alíquota decrescente (EC 132/2023)
    public string? CstIcms { get; private set; }
    public string? CsosnIcms { get; private set; }
    public decimal BaseIcms { get; private set; }
    public decimal AliquotaIcms { get; private set; }
    public decimal ValorIcms { get; private set; }

    // PIS/COFINS — extintos progressivamente até 2032 (EC 132/2023)
    public string? CstPisCofins { get; private set; }
    public decimal AliquotaPis { get; private set; }
    public decimal ValorPis { get; private set; }
    public decimal AliquotaCofins { get; private set; }
    public decimal ValorCofins { get; private set; }

    // ── REFORMA TRIBUTÁRIA (EC 132/2023) ─────────────────────────────────
    // Campos prontos para receber valores quando SEFAZ publicar o novo leiaute XML.
    // Vigência gradual: 2026 (0,1%) → plena 2033. ICMS/PIS/COFINS convivem até 2032.

    // IBS = Imposto sobre Bens e Serviços (substitui ICMS estadual + ISS municipal)
    public decimal AliquotaIbs { get; private set; }
    public decimal BaseIbs { get; private set; }
    public decimal ValorIbs { get; private set; }

    // CBS = Contribuição sobre Bens e Serviços (substitui PIS + COFINS)
    public decimal AliquotaCbs { get; private set; }
    public decimal BaseCbs { get; private set; }
    public decimal ValorCbs { get; private set; }

    // IS = Imposto Seletivo (cigarros, bebidas alcoólicas, agrotóxicos, armas, etc.)
    public decimal AliquotaIs { get; private set; }
    public decimal ValorIs { get; private set; }

    // Split payment: valor a ser retido na liquidação financeira (2027+)
    public decimal ValorRetidoSplitPayment { get; private set; }

    private ItemNotaFiscal() { }

    public static ItemNotaFiscal Criar(Guid notaFiscalId, int numeroItem,
        string codigo, string descricao, string cfop, string unidade,
        decimal quantidade, decimal valorUnitario, decimal valorDesconto = 0,
        string? ncm = null, string? cest = null, Guid? produtoId = null,
        bool pesavel = false)
    {
        // Pesável → 3 casas decimais (KG, LT); não pesável → inteiro (UN, CX, PC…)
        var qtd = pesavel ? Math.Round(quantidade, 3) : Math.Round(quantidade, 0);
        var total = qtd * valorUnitario - valorDesconto;
        return new ItemNotaFiscal
        {
            NotaFiscalId = notaFiscalId, NumeroItem = numeroItem,
            Codigo = codigo, Descricao = descricao, Cfop = cfop,
            UnidadeMedida = unidade, Pesavel = pesavel,
            Quantidade = qtd,
            ValorUnitario = valorUnitario, ValorDesconto = valorDesconto,
            ValorTotal = total, Ncm = ncm, Cest = cest, ProdutoId = produtoId
        };
    }

    public void CalcularImpostos(string? cstIcms, string? csosnIcms, decimal aliqIcms,
        string? cstPis, decimal aliqPis, decimal aliqCofins)
    {
        CstIcms = cstIcms; CsosnIcms = csosnIcms;
        AliquotaIcms = aliqIcms;
        BaseIcms = ValorTotal;
        ValorIcms = Math.Round(BaseIcms * aliqIcms / 100, 2);

        CstPisCofins = cstPis;
        AliquotaPis = aliqPis;
        AliquotaCofins = aliqCofins;
        ValorPis = Math.Round(ValorTotal * aliqPis / 100, 2);
        ValorCofins = Math.Round(ValorTotal * aliqCofins / 100, 2);
    }

    // Chamado quando SEFAZ publicar NT com campos IBS/CBS/IS (previsto 2026).
    public void CalcularImpostosReforma(
        decimal aliqIbs, decimal aliqCbs, decimal aliqIs = 0)
    {
        AliquotaIbs = aliqIbs;
        BaseIbs = ValorTotal;
        ValorIbs = Math.Round(BaseIbs * aliqIbs / 100, 2);

        AliquotaCbs = aliqCbs;
        BaseCbs = ValorTotal;
        ValorCbs = Math.Round(BaseCbs * aliqCbs, 2);

        AliquotaIs = aliqIs;
        ValorIs = Math.Round(ValorTotal * aliqIs / 100, 2);
    }

    public void DefinirSplitPayment(decimal valorRetido)
        => ValorRetidoSplitPayment = valorRetido;
}
