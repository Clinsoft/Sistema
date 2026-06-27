using MediatR;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Estoque.Commands;

public record SalvarTabelaNutricionalCommand(
    Guid ProdutoId, string Porcao,
    decimal? Calorias, decimal? Proteinas, decimal? CarboidratosTotais,
    decimal? GordurasTotais, decimal? GordurasSaturadas, decimal? GordurasTrans,
    decimal? Sodio, decimal? FibrasDieteticas, decimal? Acucares,
    decimal? VitaminaA, decimal? VitaminaC, decimal? VitaminaD,
    decimal? Calcio, decimal? Ferro, decimal? Magnesio, decimal? Zinco,
    string? Ingredientes, string? Alergenicos,
    string? ModoConservacao, string? InformacoesAdicionais) : IRequest<Guid>;

public class SalvarTabelaNutricionalHandler(ITabelaNutricionalRepository repo, IUnitOfWork uow)
    : IRequestHandler<SalvarTabelaNutricionalCommand, Guid>
{
    public async Task<Guid> Handle(SalvarTabelaNutricionalCommand cmd, CancellationToken ct)
    {
        var tabela = await repo.ObterPorProdutoAsync(cmd.ProdutoId, ct);

        if (tabela is null)
        {
            tabela = TabelaNutricional.Criar(cmd.ProdutoId, cmd.Porcao);
            await repo.AdicionarAsync(tabela, ct);
        }

        tabela.Atualizar(cmd.Porcao, cmd.Calorias, cmd.Proteinas, cmd.CarboidratosTotais,
            cmd.GordurasTotais, cmd.Sodio, cmd.Ingredientes, cmd.Alergenicos,
            cmd.ModoConservacao, cmd.InformacoesAdicionais);

        if (tabela.Id != Guid.Empty) repo.Atualizar(tabela);
        await uow.SalvarAsync(ct);
        return tabela.Id;
    }
}
