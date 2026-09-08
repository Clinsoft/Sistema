using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Desempenho.Entities;

/// <summary>Arquivo mensal (imutável) do demonstrativo de premiação de um colaborador —
/// gerado automaticamente todo mês, com o PDF e os valores da competência congelados.</summary>
public class DemonstrativoArquivado : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid ColaboradorId { get; private set; }
    public string ColaboradorNome { get; private set; } = null!;
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public decimal Premio { get; private set; }
    public byte[] Pdf { get; private set; } = null!;
    public DateTime GeradoEm { get; private set; }

    private DemonstrativoArquivado() { }

    public static DemonstrativoArquivado Criar(Guid empresaId, Guid colaboradorId, string nome,
        int ano, int mes, decimal premio, byte[] pdf)
        => new()
        {
            EmpresaId = empresaId, ColaboradorId = colaboradorId, ColaboradorNome = nome,
            Ano = ano, Mes = mes, Premio = premio, Pdf = pdf, GeradoEm = DateTime.UtcNow,
        };

    public void Atualizar(decimal premio, byte[] pdf) { Premio = premio; Pdf = pdf; GeradoEm = DateTime.UtcNow; }
}
