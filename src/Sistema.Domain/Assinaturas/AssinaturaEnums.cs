namespace Sistema.Domain.Assinaturas;

/// <summary>Planos comercializados do Natural Sistemas (ver landing/preços).</summary>
public enum PlanoAssinatura
{
    Micro = 0,
    Essencial = 1,
    Profissional = 2,
    Rede = 3,
}

/// <summary>Estado persistido da assinatura (o que o gestor/painel define).</summary>
public enum StatusAssinatura
{
    /// <summary>Período de avaliação (14 dias) com recursos do Profissional liberados.</summary>
    Trial = 0,
    /// <summary>Pagante em dia.</summary>
    Ativa = 1,
    /// <summary>Bloqueada manualmente ou por inadimplência após tolerância.</summary>
    Bloqueada = 2,
    /// <summary>Encerrada (cliente saiu).</summary>
    Cancelada = 3,
}

public enum CicloCobranca
{
    Mensal = 0,
    Anual = 1,
}

/// <summary>Situação EFETIVA calculada a partir de status + datas (não é persistida).</summary>
public enum SituacaoAssinatura
{
    TrialAtivo,
    TrialExpirado,
    Ativa,
    EmTolerancia,   // venceu, mas dentro dos dias de graça
    Bloqueada,
    Cancelada,
}

/// <summary>Recursos do sistema que os planos liberam. Usado no gate por plano.</summary>
public enum Recurso
{
    Pdv,
    Fiscal,
    Estoque,
    Validade,
    Etiquetas,
    Financeiro,
    Whatsapp,
    MarketingIa,
    Fidelidade,
    Recebiveis,
    MultiLoja,
    DrePorLoja,
    Premiacao,
    Cotacoes,
    Concorrencia,
}
