using MediatR;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Domain.Vendas.Interfaces;

namespace Sistema.Application.Vendas.Commands;

public record FecharSessaoCommand(Guid SessaoId, decimal SaldoFechamento, string? Observacao = null)
    : IRequest<FecharSessaoResult>;

public record FecharSessaoResult(
    decimal SaldoAbertura, decimal TotalVendas, decimal TotalSuprimentos,
    decimal TotalSangrias, decimal SaldoFechamento, decimal Diferenca);

public class FecharSessaoHandler(IPDVSessaoRepository repo, IVendaRepository vendaRepo, IUnitOfWork uow)
    : IRequestHandler<FecharSessaoCommand, FecharSessaoResult>
{
    public async Task<FecharSessaoResult> Handle(FecharSessaoCommand cmd, CancellationToken ct)
    {
        var sessao = await repo.ObterPorIdAsync(cmd.SessaoId, ct)
            ?? throw new KeyNotFoundException("Sessão não encontrada.");

        if (sessao.Status == Domain.Vendas.Entities.StatusSessao.Fechada)
            throw new InvalidOperationException("Sessão já está fechada.");

        // Total de vendas em dinheiro da sessão
        var inicio = sessao.Abertura;
        var fim = DateTime.Now;
        var totalVendas = await vendaRepo.TotalVendidasAsync(sessao.EmpresaId, inicio, fim,
            sessao.UsuarioId, sessao.LocalEstoqueId, ct);
        // Só o dinheiro fica na gaveta (cartão/pix/crediário não). E do dinheiro
        // recebido é preciso descontar o troco devolvido ao cliente.
        var (dinheiro, troco) = await vendaRepo.TotaisDinheiroAsync(sessao.EmpresaId, inicio, fim,
            sessao.UsuarioId, sessao.LocalEstoqueId, ct);

        sessao.Fechar(cmd.SaldoFechamento, cmd.Observacao);
        repo.Atualizar(sessao);
        await uow.SalvarAsync(ct);

        var saldoEsperado = sessao.SaldoAbertura + (dinheiro - troco) + sessao.TotalSuprimentos - sessao.TotalSangrias;
        return new FecharSessaoResult(
            sessao.SaldoAbertura, totalVendas,
            sessao.TotalSuprimentos, sessao.TotalSangrias,
            cmd.SaldoFechamento,
            cmd.SaldoFechamento - saldoEsperado);
    }
}
