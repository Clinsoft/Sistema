using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Marketing.Entities;

/// <summary>Membro do Clube de Promoções (programa de fidelidade com cashback).</summary>
public class MembroClube : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ClienteId { get; private set; }
    public string Status { get; private set; } = "Ativo";   // Ativo | Inativo
    public DateTime DataAdesao { get; private set; }
    public string? Observacao { get; private set; }

    public decimal SaldoCashback { get; private set; }      // disponível para uso
    public decimal TotalCashback { get; private set; }      // total já creditado (histórico)
    public decimal TotalCompras { get; private set; }

    private MembroClube() { }

    public static MembroClube Criar(Guid empresaId, Guid clienteId, string status,
        DateTime dataAdesao, string? observacao)
        => new()
        {
            EmpresaId = empresaId,
            ClienteId = clienteId,
            Status = status,
            DataAdesao = dataAdesao,
            Observacao = observacao,
        };

    public void Editar(string status, DateTime dataAdesao, string? observacao)
    {
        Status = status;
        DataAdesao = dataAdesao;
        Observacao = observacao;
        AtualizadoEm = DateTime.UtcNow;
    }

    public void Creditar(decimal valor)
    {
        if (valor <= 0) return;
        SaldoCashback += valor;
        TotalCashback += valor;
        AtualizadoEm = DateTime.UtcNow;
    }

    public void Debitar(decimal valor)
    {
        if (valor <= 0) return;
        SaldoCashback = Math.Max(0, SaldoCashback - valor);
        AtualizadoEm = DateTime.UtcNow;
    }

    public void RegistrarCompra(decimal valor)
    {
        if (valor > 0) TotalCompras += valor;
    }
}

/// <summary>Movimento de cashback (crédito/débito) de um membro do clube.</summary>
public class MovimentoCashback : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid MembroClubeId { get; private set; }
    public Guid ClienteId { get; private set; }
    public string Tipo { get; private set; } = "Credito";  // Credito | Debito
    public decimal Valor { get; private set; }
    public string? Motivo { get; private set; }
    public DateTime Data { get; private set; }

    // Origem em venda (preenche a aba Histórico)
    public string? VendaNumero { get; private set; }
    public decimal DescontoUsado { get; private set; }

    private MovimentoCashback() { }

    public static MovimentoCashback Criar(Guid empresaId, Guid membroId, Guid clienteId,
        string tipo, decimal valor, string? motivo,
        string? vendaNumero = null, decimal descontoUsado = 0)
        => new()
        {
            EmpresaId = empresaId,
            MembroClubeId = membroId,
            ClienteId = clienteId,
            Tipo = tipo,
            Valor = valor,
            Motivo = motivo,
            Data = DateTime.UtcNow,
            VendaNumero = vendaNumero,
            DescontoUsado = descontoUsado,
        };
}

/// <summary>Configuração do Clube de Promoções (regras de cashback e benefícios).</summary>
public class ConfiguracaoClube : Entity
{
    public Guid EmpresaId { get; private set; }
    public decimal PercentualCashback { get; private set; } = 5;
    public int Validade { get; private set; } = 180;            // dias; 0 = sem validade
    public decimal MinimoResgate { get; private set; } = 10;
    public decimal LimiteUsoPercent { get; private set; } = 50;
    public decimal DescontoMembro { get; private set; }
    public bool AniversarianteDuplo { get; private set; } = true;
    public bool Ativo { get; private set; } = true;
    public string NomeClubeExibicao { get; private set; } = "Clube de Promoções";

    private ConfiguracaoClube() { }

    public static ConfiguracaoClube Padrao(Guid empresaId) => new() { EmpresaId = empresaId };

    public void Atualizar(decimal percentualCashback, int validade, decimal minimoResgate,
        decimal limiteUsoPercent, decimal descontoMembro, bool aniversarianteDuplo,
        bool ativo, string nomeClubeExibicao)
    {
        PercentualCashback = percentualCashback;
        Validade = validade;
        MinimoResgate = minimoResgate;
        LimiteUsoPercent = limiteUsoPercent;
        DescontoMembro = descontoMembro;
        AniversarianteDuplo = aniversarianteDuplo;
        Ativo = ativo;
        NomeClubeExibicao = string.IsNullOrWhiteSpace(nomeClubeExibicao) ? "Clube de Promoções" : nomeClubeExibicao;
        AtualizadoEm = DateTime.UtcNow;
    }
}
