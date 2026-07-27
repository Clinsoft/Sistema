using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Contabilidade.Entities;

public class Contador : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string CpfCnpj { get; private set; } = null!;   // CPF ou CNPJ do contador/escritório
    public string Email { get; private set; } = null!;
    public string? Telefone { get; private set; }
    public string? CRC { get; private set; }                // Registro CRC
    public Guid? FornecedorId { get; private set; }          // fornecedor de honorários vinculado (ex.: INOVA)
    public bool Ativo { get; private set; } = true;
    // CriadoEm é herdado de Entity (já com default UtcNow) — não redeclarar aqui,
    // pois a duplicação causa "Ambiguous match found" por reflexão ao salvar.

    private Contador() { }

    public static Contador Criar(Guid empresaId, string nome, string cpfCnpj, string email,
        string? telefone = null, string? crc = null, Guid? fornecedorId = null)
        => new()
        {
            EmpresaId = empresaId,
            Nome = nome,
            CpfCnpj = cpfCnpj.Replace(".", "").Replace("-", "").Replace("/", ""),
            Email = email,
            Telefone = telefone,
            CRC = crc,
            FornecedorId = fornecedorId,
        };

    public void Editar(string nome, string email, string? telefone, string? crc, Guid? fornecedorId = null)
    {
        Nome = nome;
        Email = email;
        Telefone = telefone;
        CRC = crc;
        FornecedorId = fornecedorId;
    }

    public void Desativar() => Ativo = false;
    public void Reativar() => Ativo = true;
}
