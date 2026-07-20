using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Financeiro.Entities;

/// <summary>Conta a Pagar ou Receber — representa um título financeiro com parcelas.</summary>
public class LancamentoFinanceiro : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid? ClienteId { get; private set; }
    public Guid? FornecedorId { get; private set; }
    public Guid? ColaboradorId { get; private set; }   // beneficiário colaborador (ex.: salário)
    public Guid? ContaBancariaId { get; private set; }
    public Guid? CategoriaId { get; private set; }
    public TipoLancamento Tipo { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal ValorOriginal { get; private set; }
    public decimal ValorPago { get; private set; }
    public DateTime DataEmissao { get; private set; }
    public DateTime DataVencimento { get; private set; }
    public DateTime? DataPagamento { get; private set; }
    public StatusLancamento Status { get; private set; }
    public string? DocumentoOrigem { get; private set; }   // Nº NF, Venda, etc.
    public string? Observacao { get; private set; }
    public string? Categoria { get; private set; }         // subcategoria (texto): Vendas, Serviços, Despesas Administrativas…
    public string? ClienteNome { get; private set; }       // nome do pagador/cliente informado manualmente
    public int Parcela { get; private set; } = 1;
    public int TotalParcelas { get; private set; } = 1;
    public string? GrupoParcelamento { get; private set; } // GUID para agrupar parcelas do mesmo título

    private LancamentoFinanceiro() { }

    public static LancamentoFinanceiro Criar(Guid empresaId, TipoLancamento tipo,
        string descricao, decimal valor, DateTime dataVencimento,
        Guid? clienteId = null, Guid? fornecedorId = null, Guid? categoriaId = null,
        Guid? contaBancariaId = null, string? documentoOrigem = null,
        int parcela = 1, int totalParcelas = 1, string? grupoParcelamento = null,
        Guid? colaboradorId = null)
        => new()
        {
            EmpresaId = empresaId, Tipo = tipo, Descricao = descricao,
            ValorOriginal = valor, DataEmissao = DateTime.Today,
            DataVencimento = dataVencimento, Status = StatusLancamento.EmAberto,
            ClienteId = clienteId, FornecedorId = fornecedorId, ColaboradorId = colaboradorId,
            CategoriaId = categoriaId, ContaBancariaId = contaBancariaId,
            DocumentoOrigem = documentoOrigem,
            Parcela = parcela, TotalParcelas = totalParcelas,
            GrupoParcelamento = grupoParcelamento ?? Guid.NewGuid().ToString()
        };

    public void DefinirClassificacao(string? categoria, string? clienteNome, string? observacao)
    {
        Categoria = categoria;
        ClienteNome = clienteNome;
        Observacao = observacao;
    }

    public void Baixar(decimal valorPago, DateTime dataPagamento, Guid? contaBancariaId = null)
    {
        if (Status == StatusLancamento.Pago)
            throw new InvalidOperationException("Lançamento já está pago.");

        ValorPago = valorPago;
        DataPagamento = dataPagamento;
        ContaBancariaId = contaBancariaId ?? ContaBancariaId;
        Status = valorPago >= ValorOriginal ? StatusLancamento.Pago : StatusLancamento.PagoParcialmente;
    }

    public void Cancelar()
    {
        if (Status == StatusLancamento.Pago)
            throw new InvalidOperationException("Não é possível cancelar um lançamento já pago.");
        Status = StatusLancamento.Cancelado;
    }

    /// <summary>
    /// Define o fornecedor do lançamento. Usado para corrigir contas a pagar
    /// geradas por escriturações que ficaram sem fornecedor vinculado.
    /// </summary>
    public void DefinirFornecedor(Guid fornecedorId) => FornecedorId = fornecedorId;

    /// <summary>
    /// Define o beneficiário: fornecedor OU colaborador (nunca os dois). Passar um
    /// deles zera o outro, para o benefíciário ficar sempre consistente.
    /// </summary>
    public void DefinirBeneficiario(Guid? fornecedorId, Guid? colaboradorId)
    {
        FornecedorId = fornecedorId;
        ColaboradorId = colaboradorId;
    }

    public void Editar(string descricao, decimal valorOriginal, DateTime dataVencimento, string? observacao,
        Guid? fornecedorId = null, Guid? colaboradorId = null)
    {
        Descricao = descricao;
        ValorOriginal = valorOriginal;
        DataVencimento = dataVencimento;
        Observacao = observacao;
        if (fornecedorId.HasValue || colaboradorId.HasValue)
        {
            FornecedorId = fornecedorId;
            ColaboradorId = colaboradorId;
        }
    }

    public void Renegociar(decimal novoValor, DateTime novoVencimento, string? observacao = null)
    {
        ValorOriginal = novoValor;
        DataVencimento = novoVencimento;
        Status = StatusLancamento.EmAberto;
        Observacao = observacao;
    }

    public decimal Saldo => ValorOriginal - ValorPago;

    public bool Vencido => Status == StatusLancamento.EmAberto && DataVencimento < DateTime.Today;
}

public enum TipoLancamento { ContaReceber, ContaPagar }

public enum StatusLancamento { EmAberto, PagoParcialmente, Pago, Cancelado, Renegociado }
