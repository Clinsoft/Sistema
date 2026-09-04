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
    /// Inscreve automaticamente na primeira compra e acumula o total comprado.</summary>
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
