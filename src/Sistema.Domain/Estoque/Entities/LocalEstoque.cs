using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class LocalEstoque : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public bool Principal { get; private set; }
    public bool Ativo { get; private set; } = true;

    // Endereço da unidade (para entrega/pedido de compra)
    public string? Logradouro { get; private set; }
    public string? Numero { get; private set; }
    public string? Complemento { get; private set; }
    public string? Bairro { get; private set; }
    public string? Cidade { get; private set; }
    public string? Uf { get; private set; }
    public string? Cep { get; private set; }
    public string? Telefone { get; private set; }

    private LocalEstoque() { }

    public static LocalEstoque Criar(Guid empresaId, string nome, bool principal = false, string? descricao = null)
        => new() { EmpresaId = empresaId, Nome = nome, Principal = principal, Descricao = descricao };

    public void Editar(string nome, bool principal, string? descricao)
    {
        Nome = nome;
        Principal = principal;
        Descricao = descricao;
    }

    /// <summary>Define/atualiza o endereço da unidade.</summary>
    public void DefinirEndereco(string? logradouro, string? numero, string? complemento,
        string? bairro, string? cidade, string? uf, string? cep, string? telefone)
    {
        Logradouro = logradouro; Numero = numero; Complemento = complemento;
        Bairro = bairro; Cidade = cidade; Uf = uf; Cep = cep; Telefone = telefone;
    }

    /// <summary>Endereço em uma linha, para mensagens (ex.: WhatsApp do pedido).</summary>
    public string? EnderecoFormatado()
    {
        var l1 = string.Join(", ", new[]
        {
            string.Join(" ", new[] { Logradouro, Numero }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Complemento, Bairro
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var l2 = string.Join(" ", new[]
        {
            string.Join(string.IsNullOrWhiteSpace(Uf) ? "" : "/", new[] { Cidade, Uf }.Where(s => !string.IsNullOrWhiteSpace(s))),
            string.IsNullOrWhiteSpace(Cep) ? "" : $"CEP {Cep}"
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var full = string.Join(" - ", new[] { l1, l2 }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(full) ? null : full;
    }

    /// <summary>Reassocia o local a outra filial (empresa) — ex.: cliente abriu nova filial.</summary>
    public void Reassociar(Guid empresaId) => EmpresaId = empresaId;
}
