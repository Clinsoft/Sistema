using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Cadastros.Entities;

public class Cliente : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? CpfCnpj { get; private set; }
    public TipoPessoa TipoPessoa { get; private set; }
    public string? Email { get; private set; }
    public string? Telefone { get; private set; }
    public string? Celular { get; private set; }
    public DateTime? DataNascimento { get; private set; }
    public string? Logradouro { get; private set; }
    public string? Numero { get; private set; }
    public string? Complemento { get; private set; }
    public string? Bairro { get; private set; }
    public string? Cidade { get; private set; }
    public string? Uf { get; private set; }
    public string? Cep { get; private set; }
    public string? Classificacao { get; private set; }
    public string? Observacao { get; private set; }
    public decimal LimiteCredito { get; private set; }
    public bool Ativo { get; private set; } = true;
    public int PontosFidelidade { get; private set; }

    private Cliente() { }

    public static Cliente Criar(Guid empresaId, string nome, TipoPessoa tipoPessoa,
        string? cpfCnpj = null, string? email = null, string? telefone = null,
        string? celular = null, DateTime? dataNascimento = null)
        => new()
        {
            EmpresaId = empresaId,
            Nome = nome,
            TipoPessoa = tipoPessoa,
            CpfCnpj = cpfCnpj,
            Email = email,
            Telefone = telefone,
            Celular = celular,
            DataNascimento = dataNascimento
        };

    public void AdicionarEndereco(string logradouro, string numero, string bairro,
        string cidade, string uf, string cep, string? complemento = null)
    {
        Logradouro = logradouro; Numero = numero; Bairro = bairro;
        Cidade = cidade; Uf = uf; Cep = cep; Complemento = complemento;
    }

    public void AtualizarDados(string nome, string? email, string? telefone, string? celular,
        string? logradouro, string? numero, string? complemento, string? bairro,
        string? cidade, string? uf, string? cep, decimal limiteCredito, string? classificacao)
    {
        Nome = nome; Email = email; Telefone = telefone; Celular = celular;
        Logradouro = logradouro; Numero = numero; Complemento = complemento;
        Bairro = bairro; Cidade = cidade; Uf = uf; Cep = cep;
        LimiteCredito = limiteCredito; Classificacao = classificacao;
    }

    public void AdicionarPontos(int pontos) => PontosFidelidade += pontos;
    public void RetirarPontos(int pontos) => PontosFidelidade = Math.Max(0, PontosFidelidade - pontos);
    public void Desativar() => Ativo = false;
    public void Reativar() => Ativo = true;
}

public enum TipoPessoa { Fisica, Juridica }
