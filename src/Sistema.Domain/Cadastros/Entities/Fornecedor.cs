using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Cadastros.Entities;

public class Fornecedor : Entity
{
    public Guid EmpresaId { get; private set; }
    public string RazaoSocial { get; private set; } = null!;
    public string? NomeFantasia { get; private set; }
    public string Cnpj { get; private set; } = null!;
    public string? InscricaoEstadual { get; private set; }
    public string? Email { get; private set; }
    public string? Telefone { get; private set; }
    public string? Contato { get; private set; }
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

    private Fornecedor() { }

    public static Fornecedor Criar(Guid empresaId, string razaoSocial, string cnpj,
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
        string? cep = null, string? inscricaoEstadual = null, string? observacao = null)
    {
        RazaoSocial = razaoSocial;
        NomeFantasia = nomeFantasia;
        Email = email;
        Telefone = telefone;
        Contato = contato;
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

    public void Desativar() => Ativo = false;
    public void Reativar() => Ativo = true;
}
