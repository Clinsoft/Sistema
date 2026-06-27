using MediatR;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Estoque.Commands;

public record EditarProdutoCommand(
    Guid EmpresaId, Guid ProdutoId, string Descricao,
    Guid CategoriaId, Guid MarcaId, Guid UnidadeMedidaId,
    decimal CustoUnitario, decimal PrecoVenda,
    string? CodigoBarras = null, string? Ncm = null,
    decimal EstoqueMinimo = 0, bool Ativo = true) : IRequest;

public class EditarProdutoHandler(IProdutoRepository repo, IUnitOfWork uow) : IRequestHandler<EditarProdutoCommand>
{
    public async Task Handle(EditarProdutoCommand cmd, CancellationToken ct)
    {
        var produto = await repo.ObterPorIdAsync(cmd.ProdutoId, ct)
            ?? throw new KeyNotFoundException($"Produto {cmd.ProdutoId} não encontrado.");

        produto.Editar(cmd.Descricao, cmd.CategoriaId, cmd.MarcaId, cmd.UnidadeMedidaId,
            cmd.CodigoBarras, cmd.Ncm, cmd.EstoqueMinimo, cmd.Ativo);

        if (cmd.CustoUnitario != produto.CustoUnitario || cmd.PrecoVenda != produto.PrecoVenda)
            produto.AtualizarPreco(cmd.CustoUnitario, cmd.PrecoVenda);

        repo.Atualizar(produto);
        await uow.SalvarAsync(ct);
    }
}
