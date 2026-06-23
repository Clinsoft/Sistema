using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Crediario.Entities;

public class RenegociacaoCrediario : Entity
{
    public Guid CrediarioOrigemId { get; private set; }
    public Guid CrediarioNovoId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public decimal SaldoRenegociado { get; private set; }
    public decimal Desconto { get; private set; }
    public string? Motivo { get; private set; }

    private RenegociacaoCrediario() { }

    public static RenegociacaoCrediario Criar(Guid crediarioOrigemId, Guid crediarioNovoId,
        Guid usuarioId, decimal saldoRenegociado, decimal desconto = 0, string? motivo = null)
        => new()
        {
            CrediarioOrigemId = crediarioOrigemId,
            CrediarioNovoId = crediarioNovoId,
            UsuarioId = usuarioId,
            SaldoRenegociado = saldoRenegociado,
            Desconto = desconto,
            Motivo = motivo
        };
}
