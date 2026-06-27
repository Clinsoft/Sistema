using FluentValidation;
using MediatR;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Estoque.Commands;

public record RegistrarMovimentacaoCommand(
    Guid EmpresaId, Guid ProdutoId, Guid LocalEstoqueId,
    string Tipo, decimal Quantidade, decimal CustoUnitario,
    Guid? LoteId = null, string? DocumentoOrigem = null,
    Guid? UsuarioId = null, string? Observacao = null) : IRequest<Guid>;

public class RegistrarMovimentacaoValidator : AbstractValidator<RegistrarMovimentacaoCommand>
{
    private static readonly string[] TiposValidos =
        Enum.GetNames<TipoMovimentacao>();

    public RegistrarMovimentacaoValidator()
    {
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.CustoUnitario).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Tipo).Must(t => TiposValidos.Contains(t))
            .WithMessage($"Tipo inválido. Valores: {string.Join(", ", TiposValidos)}");
    }
}

public class RegistrarMovimentacaoHandler(
    IMovimentacaoEstoqueRepository movRepo,
    IProdutoRepository produtoRepo,
    IUnitOfWork uow)
    : IRequestHandler<RegistrarMovimentacaoCommand, Guid>
{
    public async Task<Guid> Handle(RegistrarMovimentacaoCommand cmd, CancellationToken ct)
    {
        var produto = await produtoRepo.ObterPorIdAsync(cmd.ProdutoId, ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        var tipo = Enum.Parse<TipoMovimentacao>(cmd.Tipo);

        var mov = MovimentacaoEstoque.Criar(
            cmd.EmpresaId, cmd.ProdutoId, cmd.LocalEstoqueId,
            tipo, cmd.Quantidade, cmd.CustoUnitario,
            cmd.LoteId, cmd.DocumentoOrigem, cmd.UsuarioId, cmd.Observacao);

        // Atualiza estoque no produto
        switch (tipo)
        {
            case TipoMovimentacao.Entrada:
            case TipoMovimentacao.AjustePositivo:
            case TipoMovimentacao.Devolucao:
                produto.AjustarEstoque(cmd.Quantidade);
                break;
            case TipoMovimentacao.Saida:
            case TipoMovimentacao.AjusteNegativo:
                produto.AjustarEstoque(-cmd.Quantidade);
                break;
        }

        await movRepo.AdicionarAsync(mov, ct);
        produtoRepo.Atualizar(produto);
        await uow.SalvarAsync(ct);
        return mov.Id;
    }
}
