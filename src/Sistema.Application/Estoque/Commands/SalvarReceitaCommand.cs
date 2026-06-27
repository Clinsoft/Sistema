using MediatR;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Estoque.Commands;

public record SalvarReceitaCommand(
    Guid ProdutoId, string Titulo, string Ingredientes, string ModoPreparo,
    string? Descricao = null, string? Dicas = null,
    int? TempoPreparoMinutos = null, int? Porcoes = null,
    string? UrlFoto = null, int Ordem = 0) : IRequest<Guid>;

public record AdicionarSugestaoCommand(
    Guid ProdutoId, string Titulo, string Descricao, string Tipo,
    Guid? ProdutoRelacionadoId = null, int Ordem = 0) : IRequest<Guid>;

public class SalvarReceitaHandler(IReceitaProdutoRepository repo, IUnitOfWork uow)
    : IRequestHandler<SalvarReceitaCommand, Guid>
{
    public async Task<Guid> Handle(SalvarReceitaCommand cmd, CancellationToken ct)
    {
        var receita = ReceitaProduto.Criar(cmd.ProdutoId, cmd.Titulo, cmd.Ingredientes, cmd.ModoPreparo, cmd.Ordem);
        await repo.AdicionarAsync(receita, ct);
        await uow.SalvarAsync(ct);
        return receita.Id;
    }
}

public class AdicionarSugestaoHandler(ISugestaoProdutoRepository repo, IUnitOfWork uow)
    : IRequestHandler<AdicionarSugestaoCommand, Guid>
{
    public async Task<Guid> Handle(AdicionarSugestaoCommand cmd, CancellationToken ct)
    {
        var tipo = Enum.Parse<TipoSugestao>(cmd.Tipo);
        var sugestao = SugestaoProduto.Criar(cmd.ProdutoId, cmd.Titulo, cmd.Descricao, tipo, cmd.Ordem, cmd.ProdutoRelacionadoId);
        await repo.AdicionarAsync(sugestao, ct);
        await uow.SalvarAsync(ct);
        return sugestao.Id;
    }
}
