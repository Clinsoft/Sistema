using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Estoque.Entities;

public class UnidadeMedida : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Sigla { get; private set; } = null!;
    public string Descricao { get; private set; } = null!;
    /// <summary>
    /// Pesável = contagem com 3 casas decimais (KG, LT, etc.).
    /// Não pesável = quantidade inteira (UN, CX, PC, etc.).
    /// </summary>
    public bool Pesavel { get; private set; }
    public bool Ativo { get; private set; } = true;

    private UnidadeMedida() { }

    public static UnidadeMedida Criar(Guid empresaId, string sigla, string descricao, bool pesavel = false)
        => new() { EmpresaId = empresaId, Sigla = sigla.ToUpper(), Descricao = descricao, Pesavel = pesavel };

    public void Editar(string sigla, string descricao, bool pesavel)
    {
        Sigla = sigla.ToUpper();
        Descricao = descricao;
        Pesavel = pesavel;
    }

    // Unidades padrão para seed por empresa
    public static readonly (string Sigla, string Descricao, bool Pesavel)[] Padroes =
    [
        ("CX",   "Caixa",    false),
        ("CJ",   "Conjunto", false),
        ("DISP", "Display",  false),
        ("HR",   "Hora",     false),
        ("JG",   "Jogo",     false),
        ("KG",   "Kilo",     true),
        ("LT",   "Litro",    true),
        ("PT",   "Pacote",   false),
        ("PC",   "Peça",     false),
        ("UN",   "Unidade",  false),
    ];
}
