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

    /// <summary>Código IBGE do município (7 dígitos). Obrigatório para NF-e.</summary>
    public string? CodMunicipio { get; private set; }

    /// <summary>Nome do município conforme tabela IBGE. Obrigatório para NF-e.</summary>
    public string? NomeMunicipio { get; private set; }

    // Multi-filial
    public Guid? MatrizId { get; private set; }
    public string TipoUnidade { get; private set; } = "Matriz"; // Matriz | Filial

    // Encargos de venda (% sobre o preço de venda) — usados na formação de preço
    // para calcular a MARGEM LÍQUIDA (margem bruta menos estes encargos).
    public decimal TaxaImpostoVenda { get; private set; }   // DAS Simples efetivo (ex.: 8,45)
    public decimal TaxaCartao { get; private set; }         // taxa média de cartão
    public decimal TaxaComissao { get; private set; }       // comissão de venda
    public decimal TaxaCustoFixo { get; private set; }      // rateio de custo fixo (% do faturamento)
    public decimal FaturamentoMedioMensal { get; private set; } // base p/ ratear o custo fixo em %

    /// <summary>Soma dos encargos de venda (% sobre o preço).</summary>
    public decimal EncargosVendaTotal => TaxaImpostoVenda + TaxaCartao + TaxaComissao + TaxaCustoFixo;

    public void DefinirEncargosVenda(decimal imposto, decimal cartao, decimal comissao,
        decimal custoFixo, decimal faturamentoMedio)
    {
        TaxaImpostoVenda = imposto;
        TaxaCartao = cartao;
        TaxaComissao = comissao;
        TaxaCustoFixo = custoFixo;
        FaturamentoMedioMensal = faturamentoMedio;
    }

    private Empresa() { }

    public void Atualizar(string razaoSocial, string nomeFantasia,
        string regimeTributario, string logradouro, string numero, string? complemento,
        string bairro, string cidade, string uf, string cep,
        string telefone, string email,
        string inscricaoEstadual = "", string inscricaoMunicipal = "", string? cnpj = null)
    {
        RazaoSocial = razaoSocial;
        NomeFantasia = nomeFantasia;
        // CNPJ só é alterado quando informado (ex.: filial que sai do CNPJ provisório
        // para o definitivo). Sem valor, preserva o atual.
        if (!string.IsNullOrWhiteSpace(cnpj)) Cnpj = cnpj;
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

    public static Empresa CriarFilial(Guid matrizId, string razaoSocial, string nomeFantasia,
        string cnpj, string regimeTributario, string logradouro, string numero, string bairro,
        string cidade, string uf, string cep, string telefone, string email,
        string inscricaoEstadual = "", string inscricaoMunicipal = "", string? complemento = null)
    {
        var filial = Criar(razaoSocial, nomeFantasia, cnpj, regimeTributario, logradouro, numero,
            bairro, cidade, uf, cep, telefone, email, inscricaoEstadual, inscricaoMunicipal, complemento);
        filial.MatrizId = matrizId;
        filial.TipoUnidade = "Filial";
        return filial;
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
