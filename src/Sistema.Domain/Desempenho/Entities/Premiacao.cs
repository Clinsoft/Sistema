using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Desempenho.Entities;

/// <summary>Regras gerais do Regulamento de Premiação por Desempenho (uma por empresa).</summary>
public class ConfiguracaoPremiacao : Entity
{
    public Guid EmpresaId { get; private set; }
    public decimal ValorBase { get; private set; } = 400m;        // prêmio integral
    public decimal RedutorPercent { get; private set; } = 80m;    // loja 90-99% → 80% do base (=320)
    public decimal MinPresenca { get; private set; } = 95m;       // % mínimo de presença
    public decimal ThresholdLoja { get; private set; } = 90m;     // % mínimo p/ ativar a loja
    public decimal ThresholdIndividual { get; private set; } = 90m;
    // Meta dinâmica: meta da loja = faturamento médio dos últimos MesesBaseMeta meses × FatorMetaLoja.
    public decimal FatorMetaLoja { get; private set; } = 90m;     // % do faturamento base (90% ≈ espírito do termo)
    public int MesesBaseMeta { get; private set; } = 1;           // nº de meses anteriores para a base
    public bool Ativo { get; private set; } = true;

    private ConfiguracaoPremiacao() { }
    public static ConfiguracaoPremiacao Padrao(Guid empresaId) => new() { EmpresaId = empresaId };

    public void Atualizar(decimal valorBase, decimal redutorPercent, decimal minPresenca,
        decimal thresholdLoja, decimal thresholdIndividual, decimal fatorMetaLoja, int mesesBaseMeta, bool ativo)
    {
        ValorBase = valorBase; RedutorPercent = redutorPercent; MinPresenca = minPresenca;
        ThresholdLoja = thresholdLoja; ThresholdIndividual = thresholdIndividual;
        FatorMetaLoja = fatorMetaLoja; MesesBaseMeta = mesesBaseMeta < 1 ? 1 : mesesBaseMeta; Ativo = ativo;
        AtualizadoEm = DateTime.UtcNow;
    }
}

/// <summary>Override manual de meta por loja para uma competência específica (ano/mês).
/// Quando não existe, a meta é calculada automaticamente da base financeira.</summary>
public class MetaPremiacaoLoja : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public decimal MetaLoja { get; private set; }
    public decimal MetaIndividual { get; private set; }

    private MetaPremiacaoLoja() { }
    public static MetaPremiacaoLoja Criar(Guid empresaId, Guid localEstoqueId, int ano, int mes, decimal metaLoja, decimal metaIndividual)
        => new() { EmpresaId = empresaId, LocalEstoqueId = localEstoqueId, Ano = ano, Mes = mes, MetaLoja = metaLoja, MetaIndividual = metaIndividual };

    public void Atualizar(decimal metaLoja, decimal metaIndividual)
    {
        MetaLoja = metaLoja; MetaIndividual = metaIndividual; AtualizadoEm = DateTime.UtcNow;
    }
}

/// <summary>Nível de execução de um item da Performance Comercial.</summary>
public enum NivelItem { NaoRealizado = 0, Parcial = 50, Consistente = 100 }

/// <summary>Avaliação semanal da Performance Comercial (100 pontos) de um colaborador.</summary>
public class AvaliacaoDesempenhoSemanal : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public Guid ColaboradorId { get; private set; }
    public DateTime InicioSemana { get; private set; }   // segunda-feira da semana avaliada
    public int Ano { get; private set; }
    public int Mes { get; private set; }

    // 1. Processo de Venda (40) — 8 pts cada
    public NivelItem Abordagem { get; private set; }
    public NivelItem Diagnostico { get; private set; }
    public NivelItem ConexaoProduto { get; private set; }
    public NivelItem SugestaoComplementar { get; private set; }
    public NivelItem Fechamento { get; private set; }
    // 2. Execução Operacional (30) — 10 pts cada
    public NivelItem Abastecimento { get; private set; }
    public NivelItem Organizacao { get; private set; }
    public NivelItem Rotina { get; private set; }
    // 3. Qualidade da Operação (30) — 10 pts cada
    public NivelItem Validade { get; private set; }
    public NivelItem Perdas { get; private set; }
    public NivelItem Armazenamento { get; private set; }

    public string? Observacao { get; private set; }

    private AvaliacaoDesempenhoSemanal() { }

    public static AvaliacaoDesempenhoSemanal Criar(Guid empresaId, Guid localEstoqueId, Guid colaboradorId, DateTime inicioSemana)
        => new()
        {
            EmpresaId = empresaId, LocalEstoqueId = localEstoqueId, ColaboradorId = colaboradorId,
            InicioSemana = inicioSemana.Date, Ano = inicioSemana.Year, Mes = inicioSemana.Month,
        };

    public void Definir(
        NivelItem abordagem, NivelItem diagnostico, NivelItem conexao, NivelItem sugestao, NivelItem fechamento,
        NivelItem abastecimento, NivelItem organizacao, NivelItem rotina,
        NivelItem validade, NivelItem perdas, NivelItem armazenamento, string? observacao)
    {
        Abordagem = abordagem; Diagnostico = diagnostico; ConexaoProduto = conexao;
        SugestaoComplementar = sugestao; Fechamento = fechamento;
        Abastecimento = abastecimento; Organizacao = organizacao; Rotina = rotina;
        Validade = validade; Perdas = perdas; Armazenamento = armazenamento;
        Observacao = observacao; AtualizadoEm = DateTime.UtcNow;
    }

    private static decimal P(NivelItem n, decimal max) => max * (int)n / 100m;

    /// <summary>Pontuação total (0–100).</summary>
    public decimal Pontos =>
        P(Abordagem, 8) + P(Diagnostico, 8) + P(ConexaoProduto, 8) + P(SugestaoComplementar, 8) + P(Fechamento, 8)
        + P(Abastecimento, 10) + P(Organizacao, 10) + P(Rotina, 10)
        + P(Validade, 10) + P(Perdas, 10) + P(Armazenamento, 10);
}

/// <summary>Apuração mensal manual: elegibilidade + cortes (zero automático) de um colaborador.</summary>
public class ApuracaoMensalPremiacao : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid LocalEstoqueId { get; private set; }
    public Guid ColaboradorId { get; private set; }
    public int Ano { get; private set; }
    public int Mes { get; private set; }

    // Elegibilidade
    public decimal PresencaPercent { get; private set; } = 100m;
    public bool FaltaInjustificada { get; private set; }   // também é corte
    public bool Advertencia { get; private set; }
    public bool ExecucaoMinima { get; private set; } = true;

    // Cortes (zero automático) — FaltaInjustificada acima também conta
    public bool ProdutoVencidoExposto { get; private set; }
    public bool HigieneGrave { get; private set; }
    public bool RotinaNaoExecutada { get; private set; }
    public bool ReclamacaoRelevante { get; private set; }

    public string? Observacao { get; private set; }

    private ApuracaoMensalPremiacao() { }

    public static ApuracaoMensalPremiacao Criar(Guid empresaId, Guid localEstoqueId, Guid colaboradorId, int ano, int mes)
        => new() { EmpresaId = empresaId, LocalEstoqueId = localEstoqueId, ColaboradorId = colaboradorId, Ano = ano, Mes = mes };

    public void Definir(decimal presenca, bool faltaInjustificada, bool advertencia, bool execucaoMinima,
        bool produtoVencidoExposto, bool higieneGrave, bool rotinaNaoExecutada, bool reclamacaoRelevante, string? observacao)
    {
        PresencaPercent = presenca; FaltaInjustificada = faltaInjustificada; Advertencia = advertencia;
        ExecucaoMinima = execucaoMinima; ProdutoVencidoExposto = produtoVencidoExposto; HigieneGrave = higieneGrave;
        RotinaNaoExecutada = rotinaNaoExecutada; ReclamacaoRelevante = reclamacaoRelevante;
        Observacao = observacao; AtualizadoEm = DateTime.UtcNow;
    }

    public bool TemCorte => FaltaInjustificada || ProdutoVencidoExposto || HigieneGrave
        || RotinaNaoExecutada || ReclamacaoRelevante;
}

/// <summary>Resultado do cálculo do prêmio de um colaborador num mês (não persistido — calculado sob demanda).</summary>
public record ResultadoPremio(
    Guid ColaboradorId, string Colaborador, Guid LocalEstoqueId,
    decimal FaturamentoLoja, decimal MetaLoja, decimal PercentLoja,
    decimal VendaIndividual, decimal MetaIndividual, decimal PercentIndividual,
    decimal PerformancePercent, int SemanasAvaliadas,
    decimal BaseLoja, decimal FatorIndividual,
    bool Elegivel, bool TemCorte, string? Motivo,
    decimal Premio, decimal DescontoValidade = 0);

/// <summary>Fórmula do prêmio (cláusulas 3ª–6ª do Regulamento).</summary>
public static class CalculoPremiacao
{
    public static ResultadoPremio Calcular(
        Guid colaboradorId, string nome, Guid localEstoqueId,
        decimal faturamentoLoja, decimal metaLoja,
        decimal vendaIndividual, decimal metaIndividual,
        decimal performancePercent, int semanasAvaliadas,
        ConfiguracaoPremiacao cfg,
        ApuracaoMensalPremiacao? apuracao,
        decimal descontoValidade = 0)
    {
        var percLoja = metaLoja > 0 ? Math.Round(faturamentoLoja / metaLoja * 100, 1) : 0;
        var percInd = metaIndividual > 0 ? Math.Round(vendaIndividual / metaIndividual * 100, 1) : 0;

        // Elegibilidade (se não houver apuração lançada, considera pendente = elegível provisório).
        var elegivel = apuracao is null || (
            apuracao.PresencaPercent >= cfg.MinPresenca
            && !apuracao.FaltaInjustificada && !apuracao.Advertencia && apuracao.ExecucaoMinima);
        var temCorte = apuracao?.TemCorte ?? false;

        // Base pela ativação da loja.
        decimal baseLoja = percLoja >= 100 ? cfg.ValorBase
            : percLoja >= cfg.ThresholdLoja ? Math.Round(cfg.ValorBase * cfg.RedutorPercent / 100m, 2)
            : 0m;

        // Fator pela ativação individual.
        decimal fatorInd = percInd >= 100 ? 1m
            : percInd >= cfg.ThresholdIndividual ? Math.Round(percInd / 100m, 4)
            : 0m;

        string? motivo = null;
        decimal premio;
        if (!elegivel) { premio = 0; motivo = "Não elegível (presença/falta/advertência/execução mínima)"; }
        else if (temCorte) { premio = 0; motivo = "Corte automático (Cláusula 6ª)"; }
        else if (baseLoja == 0) { premio = 0; motivo = $"Loja abaixo de {cfg.ThresholdLoja:0}% da meta"; }
        else if (fatorInd == 0) { premio = 0; motivo = $"Venda individual abaixo de {cfg.ThresholdIndividual:0}% da meta"; }
        else { premio = Math.Round(baseLoja * fatorInd * (performancePercent / 100m), 2); }

        return new ResultadoPremio(
            colaboradorId, nome, localEstoqueId,
            faturamentoLoja, metaLoja, percLoja,
            vendaIndividual, metaIndividual, percInd,
            performancePercent, semanasAvaliadas,
            baseLoja, fatorInd, elegivel, temCorte, motivo, premio, descontoValidade);
    }
}
