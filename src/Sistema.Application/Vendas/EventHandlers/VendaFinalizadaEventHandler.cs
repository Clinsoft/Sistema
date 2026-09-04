using MediatR;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Marketing.Entities;
using Sistema.Domain.Marketing.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Events;

namespace Sistema.Application.Vendas.EventHandlers;

public class VendaFinalizadaEventHandler(
    IProdutoRepository produtoRepo,
    IMovimentacaoEstoqueRepository movRepo,
    IClienteRepository clienteRepo,
    IClubeRepository clubeRepo,
    IUnitOfWork uow)
    : INotificationHandler<VendaFinalizadaEvent>
{
    // 1 ponto por real gasto (configurável futuramente)
    private const int PontosPorReal = 1;

    public async Task Handle(VendaFinalizadaEvent evt, CancellationToken ct)
    {
        await BaixarEstoque(evt, ct);
        await AdicionarPontosFidelidade(evt, ct);
        await GarantirMembroClube(evt, ct);
        await uow.SalvarAsync(ct);
    }

    /// <summary>Todo cliente com compra associada é membro do Clube de Promoções.
    /// Inscreve automaticamente na primeira compra, acumula o total comprado e
    /// credita o cashback conforme a configuração do clube.</summary>
    private async Task GarantirMembroClube(VendaFinalizadaEvent evt, CancellationToken ct)
    {
        if (evt.ClienteId is null) return;

        var membro = await clubeRepo.ObterMembroAsync(evt.EmpresaId, evt.ClienteId.Value, ct);
        if (membro is null)
        {
            membro = MembroClube.Criar(evt.EmpresaId, evt.ClienteId.Value,
                status: "Ativo", dataAdesao: DateTime.Today,
                observacao: "Adesão automática por compra");
            await clubeRepo.AdicionarMembroAsync(membro, ct);
        }
        membro.RegistrarCompra(evt.Total);

        // Cashback automático: credita % da compra conforme configuração do clube.
        // Só para membro Ativo e clube ativo com percentual > 0.
        if (!string.Equals(membro.Status, "Ativo", StringComparison.OrdinalIgnoreCase)) return;

        var cfg = await clubeRepo.ObterConfiguracaoAsync(evt.EmpresaId, ct);
        if (cfg is null || !cfg.Ativo || cfg.PercentualCashback <= 0) return;

        var valor = Math.Round(evt.Total * cfg.PercentualCashback / 100m, 2, MidpointRounding.AwayFromZero);
        if (valor <= 0) return;

        membro.Creditar(valor);
        await clubeRepo.AdicionarMovimentoAsync(MovimentoCashback.Criar(
            evt.EmpresaId, membro.Id, evt.ClienteId.Value,
            tipo: "Credito", valor: valor,
            motivo: $"Cashback {cfg.PercentualCashback:0.##}% da compra",
            vendaNumero: string.IsNullOrEmpty(evt.Numero) ? null : evt.Numero), ct);
    }

    private async Task BaixarEstoque(VendaFinalizadaEvent evt, CancellationToken ct)
    {
        foreach (var item in evt.Itens)
        {
            var produto = await produtoRepo.ObterPorIdAsync(item.ProdutoId, ct);
            if (produto is null) continue;

            var mov = MovimentacaoEstoque.Criar(
                evt.EmpresaId, item.ProdutoId,
                localEstoqueId: evt.LocalEstoqueId,
                TipoMovimentacao.Saida,
                item.Quantidade,
                produto.CustoUnitario,
                documentoOrigem: evt.VendaId.ToString());

            produto.AjustarEstoque(-item.Quantidade);
            await movRepo.AdicionarAsync(mov, ct);
            produtoRepo.Atualizar(produto);
        }
    }

    private async Task AdicionarPontosFidelidade(VendaFinalizadaEvent evt, CancellationToken ct)
    {
        if (evt.ClienteId is null) return;

        var cliente = await clienteRepo.ObterPorIdAsync(evt.ClienteId.Value, ct);
        if (cliente is null) return;

        var pontos = (int)Math.Floor(evt.Total * PontosPorReal);
        if (pontos > 0)
        {
            cliente.AdicionarPontos(pontos);
            clienteRepo.Atualizar(cliente);
        }
    }
}
