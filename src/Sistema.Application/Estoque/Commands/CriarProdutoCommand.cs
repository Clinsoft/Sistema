using FluentValidation;
using MediatR;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Estoque.Commands;

public record CriarProdutoCommand(
    Guid EmpresaId, string? Codigo, string Descricao,
    Guid CategoriaId, Guid MarcaId, Guid UnidadeMedidaId,
    decimal CustoUnitario, decimal PrecoVenda,
    string? CodigoBarras = null, string? Ncm = null,
    decimal EstoqueMinimo = 0, bool ControlarLote = false,
    bool ControlarValidade = false) : IRequest<Guid>;

public class CriarProdutoValidator : AbstractValidator<CriarProdutoCommand>
{
    public CriarProdutoValidator()
    {
        // Código é opcional: quando vazio, o backend gera automaticamente.
        RuleFor(x => x.Codigo).MaximumLength(30).When(x => !string.IsNullOrWhiteSpace(x.Codigo));
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustoUnitario).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PrecoVenda).GreaterThan(0);
        RuleFor(x => x.CategoriaId).NotEmpty();
        RuleFor(x => x.MarcaId).NotEmpty();
        RuleFor(x => x.UnidadeMedidaId).NotEmpty();
        RuleFor(x => x.Ncm).MaximumLength(8).When(x => x.Ncm is not null);
        RuleFor(x => x.CodigoBarras).MaximumLength(30).When(x => x.CodigoBarras is not null);
    }
}

public class CriarProdutoHandler(IProdutoRepository repo, IUnitOfWork uow)
    : IRequestHandler<CriarProdutoCommand, Guid>
{
    public async Task<Guid> Handle(CriarProdutoCommand cmd, CancellationToken ct)
    {
        // Código vazio → gera automaticamente um livre. Código informado → valida colisão.
        string codigo;
        if (string.IsNullOrWhiteSpace(cmd.Codigo))
        {
            codigo = await repo.ProximoCodigoAsync(cmd.EmpresaId, ct);
        }
        else
        {
            codigo = cmd.Codigo.Trim();
            if (await repo.ExisteAsync(p => p.EmpresaId == cmd.EmpresaId && p.Codigo == codigo, ct))
                throw new InvalidOperationException($"Já existe produto com o código '{codigo}'.");
        }

        if (cmd.CodigoBarras is not null &&
            await repo.ExisteAsync(p => p.EmpresaId == cmd.EmpresaId && p.CodigoBarras == cmd.CodigoBarras, ct))
            throw new InvalidOperationException($"Já existe produto com o código de barras '{cmd.CodigoBarras}'.");

        var produto = Produto.Criar(cmd.EmpresaId, codigo, cmd.Descricao,
            cmd.CategoriaId, cmd.MarcaId, cmd.UnidadeMedidaId,
            cmd.CustoUnitario, cmd.PrecoVenda, cmd.CodigoBarras);

        produto.DefinirEstoqueMinimo(cmd.EstoqueMinimo);

        await repo.AdicionarAsync(produto, ct);
        await uow.SalvarAsync(ct);
        return produto.Id;
    }
}
