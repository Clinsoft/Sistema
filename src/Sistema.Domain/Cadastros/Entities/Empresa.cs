using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Cadastros.Entities;

public class Empresa : Entity
{
    public string RazaoSocial { get; private set; } = null!;
    public string NomeFantasia { get; private set; } = null!;
    public string Cnpj { get; private set; } = null!;
    public string InscricaoEstadual { get; private set; } = null!;
    public string InscricaoMunicipal { get; private set; } = null!;
    public string RegimeTributario { get; private set; } = null!; // SN, LP, LR
    public string Logradouro { get; private set; } = null!;
    public string Numero { get; private set; } = null!;
    public string? Complemento { get; private set; }
    public string Bairro { get; private set; } = null!;
    public string Cidade { get; private set; } = null!;
    public string Uf { get; private set; } = null!;
    public string Cep { get; private set; } = null!;
    public string Telefone { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public bool Ativo { get; private set; } = true;

    private Empresa() { }

    public void Atualizar(string razaoSocial, string nomeFantasia,
        string regimeTributario, string logradouro, string numero, string? complemento,
        string bairro, string cidade, string uf, string cep,
        string telefone, string email,
        string inscricaoEstadual = "", string inscricaoMunicipal = "")
    {
        RazaoSocial = razaoSocial;
        NomeFantasia = nomeFantasia;
        RegimeTributario = regimeTributario;
        Logradouro = logradouro;
        Numero = numero;
        Complemento = complemento;
        Bairro = bairro;
        Cidade = cidade;
        Uf = uf;
        Cep = cep;
        Telefone = telefone;
        Email = email;
        InscricaoEstadual = inscricaoEstadual;
        InscricaoMunicipal = inscricaoMunicipal;
    }

    public static Empresa Criar(string razaoSocial, string nomeFantasia, string cnpj,
        string regimeTributario, string logradouro, string numero, string bairro,
        string cidade, string uf, string cep, string telefone, string email,
        string inscricaoEstadual = "", string inscricaoMunicipal = "", string? complemento = null)
    {
        return new Empresa
        {
            RazaoSocial = razaoSocial,
            NomeFantasia = nomeFantasia,
            Cnpj = cnpj,
            RegimeTributario = regimeTributario,
            Logradouro = logradouro,
            Numero = numero,
            Complemento = complemento,
            Bairro = bairro,
            Cidade = cidade,
            Uf = uf,
            Cep = cep,
            Telefone = telefone,
            Email = email,
            InscricaoEstadual = inscricaoEstadual,
            InscricaoMunicipal = inscricaoMunicipal
        };
    }
}
