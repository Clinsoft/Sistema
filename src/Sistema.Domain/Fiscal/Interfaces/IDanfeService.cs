using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Fiscal.Entities;

namespace Sistema.Domain.Fiscal.Interfaces;

/// <summary>
/// Geração do DANFE (Documento Auxiliar da NF-e) em PDF.
/// </summary>
public interface IDanfeService
{
    /// <summary>
    /// Gera o DANFE em formato PDF para a nota fiscal informada.
    /// </summary>
    byte[] GerarDanfe(NotaFiscal nota, Empresa empresa);
}
