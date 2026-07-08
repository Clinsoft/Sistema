using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Vendas.Entities;

/// <summary>Movimentação interna de caixa (suprimento ou sangria) registrada em uma sessão.</summary>
public class OperacaoCaixa : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid SessaoId { get; private set; }
    public TipoOperacaoCaixa Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public string? Descricao { get; private set; }
    public Guid? UsuarioId { get; private set; }

    private OperacaoCaixa() { }

    public static OperacaoCaixa Registrar(Guid empresaId, Guid sessaoId,
        TipoOperacaoCaixa tipo, decimal valor, string? descricao = null, Guid? usuarioId = null)
        => new()
        {
            EmpresaId = empresaId,
            SessaoId = sessaoId,
            Tipo = tipo,
            Valor = valor,
            Descricao = descricao,
            UsuarioId = usuarioId,
            CriadoEm = DateTime.Now
        };
}

public enum TipoOperacaoCaixa { Suprimento, Sangria }
