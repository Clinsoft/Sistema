using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Cadastros.Entities;

public class Fornecedor : Entity
{
    public Guid EmpresaId { get; private set; }
    public string RazaoSocial { get; private set; } = null!;
    public string? NomeFantasia { get; private set; }
    public string? Cnpj { get; private set; }
    public string? InscricaoEstadual { get; private set; }
    public string? Email { get; private set; }
    public string? Telefone { get; private set; }
    public string? Celular { get; private set; }
    public string? Contato { get; private set; }
    public string? Tipos { get; private set; }   // CSV: Fornecedor,Transportadora,Representante,ParceiroCom
    public string? Logradouro { get; private set; }
    public string? Numero { get; private set; }
    public string? Complemento { get; private set; }
    public string? Bairro { get; private set; }
    public string? Cidade { get; private set; }
    public string? Uf { get; private set; }
    public string? Cep { get; private set; }
    public int PrazoPagamentoDias { get; private set; }
    public string? Observacao { get; private set; }
    public bool Ativo { get; private set; } = true;

    // Mensalidade fixa (ex.: honorários do contador, aluguel) — gera conta a pagar
    // recorrente todo mês no dia de vencimento informado.
    public decimal? MensalidadeValor { get; private set; }
    public int? MensalidadeDiaVencimento { get; private set; }
    public string? MensalidadeCategoria { get; private set; }

    private Fornecedor() { }

    public static Fornecedor Criar(Guid empresaId, string razaoSocial, string? cnpj = null,
        string? nomeFantasia = null, string? email = null, string? telefone = null)
        => new()
        {
            EmpresaId = empresaId,
            RazaoSocial = razaoSocial,
            Cnpj = cnpj,
            NomeFantasia = nomeFantasia,
            Email = email,
            Telefone = telefone
        };

    public void Editar(string razaoSocial, string? nomeFantasia, string? email,
        string? telefone, string? contato, int prazoPagamentoDias,
        string? logradouro = null, string? numero = null, string? complemento = null,
        string? bairro = null, string? cidade = null, string? uf = null,
        string? cep = null, string? inscricaoEstadual = null, string? observacao = null,
        string? celular = null, string? tipos = null)
    {
        RazaoSocial = razaoSocial;
        NomeFantasia = nomeFantasia;
        Email = email;
        Telefone = telefone;
        Celular = celular;
        Contato = contato;
        Tipos = tipos;
        PrazoPagamentoDias = prazoPagamentoDias;
        Logradouro = logradouro;
        Numero = numero;
        Complemento = complemento;
        Bairro = bairro;
        Cidade = cidade;
        Uf = uf;
        Cep = cep;
        InscricaoEstadual = inscricaoEstadual;
        Observacao = observacao;
    }

    /// <summary>Configura a mensalidade fixa deste fornecedor (valor + dia de vencimento + categoria).</summary>
    public void DefinirMensalidade(decimal? valor, int? diaVencimento, string? categoria)
    {
        MensalidadeValor = valor is > 0 ? valor : null;
        MensalidadeDiaVencimento = diaVencimento is >= 1 and <= 31 ? diaVencimento : null;
        MensalidadeCategoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria;
    }

    public void Desativar() => Ativo = false;
    public void Reativar() => Ativo = true;
}
