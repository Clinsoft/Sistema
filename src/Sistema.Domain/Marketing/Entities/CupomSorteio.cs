using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Marketing.Entities;

/// <summary>
/// Cupom de um sorteio (promoção do tipo "Sorteio"). Gerado no PDV quando a venda
/// atinge o valor mínimo; guarda os dados do cliente para o sorteio/urna.
/// </summary>
public class CupomSorteio : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid PromocaoId { get; private set; }
    public Guid? LocalEstoqueId { get; private set; }
    public int Numero { get; private set; }                 // sequencial por promoção (nº na urna)
    public Guid? ClienteId { get; private set; }
    public string NomeCliente { get; private set; } = null!;
    public string? Telefone { get; private set; }
    public Guid? VendaId { get; private set; }
    public decimal ValorCompra { get; private set; }
    public bool Sorteado { get; private set; }

    private CupomSorteio() { }

    public static CupomSorteio Criar(Guid empresaId, Guid promocaoId, Guid? localEstoqueId,
        int numero, Guid? clienteId, string nomeCliente, string? telefone,
        Guid? vendaId, decimal valorCompra)
        => new()
        {
            EmpresaId = empresaId,
            PromocaoId = promocaoId,
            LocalEstoqueId = localEstoqueId,
            Numero = numero,
            ClienteId = clienteId,
            NomeCliente = nomeCliente,
            Telefone = telefone,
            VendaId = vendaId,
            ValorCompra = valorCompra,
        };

    public void MarcarSorteado() { Sorteado = true; AtualizadoEm = DateTime.UtcNow; }
}
