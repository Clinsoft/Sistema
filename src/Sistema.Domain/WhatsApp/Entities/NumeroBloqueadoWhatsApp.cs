using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.WhatsApp.Entities;

/// <summary>
/// Número bloqueado no WhatsApp: mensagens recebidas desse número são ignoradas
/// (não entram na caixa de entrada). Pode ser por loja (LocalEstoqueId) ou geral.
/// </summary>
public class NumeroBloqueadoWhatsApp : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid? LocalEstoqueId { get; private set; }   // null = todas as lojas
    public string Telefone { get; private set; } = null!; // só dígitos
    public string? Motivo { get; private set; }

    private NumeroBloqueadoWhatsApp() { }

    public static NumeroBloqueadoWhatsApp Criar(Guid empresaId, Guid? localEstoqueId, string telefone, string? motivo)
        => new()
        {
            EmpresaId = empresaId,
            LocalEstoqueId = localEstoqueId,
            Telefone = new string((telefone ?? "").Where(char.IsDigit).ToArray()),
            Motivo = motivo,
        };
}
