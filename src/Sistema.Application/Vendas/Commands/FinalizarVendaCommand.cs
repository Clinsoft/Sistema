using MediatR;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Marketing.Entities;
using Sistema.Domain.Marketing.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Entities;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Commands;

public record PagamentoDto(string Forma, decimal Valor, int Parcelas = 1,
    string? Descricao = null, Guid? OperadoraCartaoId = null);

public record FinalizarVendaCommand(
    Guid VendaId,
    IList<PagamentoDto> Pagamentos,
    string? CpfCnpjConsumidor = null,
    decimal CashbackUsado = 0) : IRequest<FinalizarVendaResult>;

/// <summary>
/// Dados para o PDV exibir comprovante e QR Code.
/// NotaFiscalId e QrCode ficam nulos enquanto certificado A1 não estiver configurado.
/// </summary>
public record FinalizarVendaResult(
    string Numero,
    decimal Total,
    decimal Troco,
    Guid? NotaFiscalId = null,
    string? QrCode = null,
    string? ChaveAcesso = null);

public class FinalizarVendaHandler(
    IVendaRepository repo, IClienteRepository clienteRepo, IClubeRepository clubeRepo, IUnitOfWork uow)
    : IRequestHandler<FinalizarVendaCommand, FinalizarVendaResult>
{
    public async Task<FinalizarVendaResult> Handle(FinalizarVendaCommand cmd, CancellationToken ct)
    {
        var venda = await repo.ObterComItensAsync(cmd.VendaId, ct)
            ?? throw new KeyNotFoundException("Venda não encontrada.");

        if (!string.IsNullOrWhiteSpace(cmd.CpfCnpjConsumidor))
            venda.InformarCpfCnpjConsumidor(cmd.CpfCnpjConsumidor);

        // Vínculo automático: se a venda não tem cliente e o CPF/CNPJ do consumidor
        // bate com um cadastro (só dígitos, ignorando máscara), associa o cliente.
        if (venda.ClienteId is null && !string.IsNullOrWhiteSpace(venda.CpfCnpjConsumidor))
        {
            var cliente = await clienteRepo.ObterPorCpfCnpjDigitosAsync(
                venda.EmpresaId, venda.CpfCnpjConsumidor, ct);
            if (cliente is not null)
                venda.VincularCliente(cliente.Id);
        }

        // Resgate de cashback (opcional): reduz a venda como desconto e debita o saldo.
        if (cmd.CashbackUsado > 0)
            await ResgatarCashback(venda, cmd.CashbackUsado, ct);

        foreach (var p in cmd.Pagamentos)
        {
            var forma = Enum.Parse<FormaPagamento>(p.Forma);
            venda.AdicionarPagamento(forma, p.Valor, p.Parcelas, p.Descricao, p.OperadoraCartaoId);
        }

        venda.Finalizar();
        // A venda vem rastreada de ObterComItensAsync: o SaveChanges já detecta os
        // pagamentos novos (INSERT) e as mudanças da venda. Chamar Update() aqui
        // marcaria os filhos novos como Modified → UPDATE de 0 linhas (concorrência).
        await uow.SalvarAsync(ct);

        // NotaFiscalId preenchido pelo EmitirNFCeHandler após publicação do evento
        return new FinalizarVendaResult(venda.Numero, venda.Total, venda.Troco,
            venda.NotaFiscalId);
    }

    /// <summary>Valida os limites do clube, aplica o cashback como desconto rateado
    /// na venda e debita o saldo do membro (registrando o movimento).</summary>
    private async Task ResgatarCashback(Venda venda, decimal solicitado, CancellationToken ct)
    {
        if (venda.ClienteId is null)
            throw new InvalidOperationException("Resgate de cashback exige um cliente associado à venda.");

        var membro = await clubeRepo.ObterMembroAsync(venda.EmpresaId, venda.ClienteId.Value, ct)
            ?? throw new InvalidOperationException("Cliente não é membro do Clube de Promoções.");
        if (!string.Equals(membro.Status, "Ativo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Membro do clube inativo.");

        var cfg = await clubeRepo.ObterConfiguracaoAsync(venda.EmpresaId, ct);
        if (cfg is null || !cfg.Ativo)
            throw new InvalidOperationException("Clube de Promoções inativo.");

        var valor = Math.Round(solicitado, 2, MidpointRounding.AwayFromZero);
        if (valor > membro.SaldoCashback)
            throw new InvalidOperationException("Saldo de cashback insuficiente.");
        if (membro.SaldoCashback < cfg.MinimoResgate)
            throw new InvalidOperationException($"Saldo abaixo do mínimo para resgate (R$ {cfg.MinimoResgate:0.00}).");

        // Limite de uso: no máximo LimiteUsoPercent% do valor da venda.
        var limite = Math.Round(venda.Total * cfg.LimiteUsoPercent / 100m, 2);
        if (limite > 0 && valor > limite)
            valor = limite;
        if (valor <= 0) return;

        var aplicado = venda.ResgatarCashback(valor);
        if (aplicado <= 0) return;

        membro.Debitar(aplicado);
        await clubeRepo.AdicionarMovimentoAsync(MovimentoCashback.Criar(
            venda.EmpresaId, membro.Id, venda.ClienteId.Value,
            tipo: "Debito", valor: aplicado,
            motivo: "Resgate de cashback na venda",
            vendaNumero: venda.Numero, descontoUsado: aplicado), ct);
    }
}
