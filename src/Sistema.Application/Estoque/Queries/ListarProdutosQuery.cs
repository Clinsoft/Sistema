using MediatR;
using Sistema.Application.Estoque.DTOs;
using Sistema.Domain.Estoque.Interfaces;

namespace Sistema.Application.Estoque.Queries;

public record ListarProdutosQuery(
    Guid EmpresaId, string? Termo = null, Guid? CategoriaId = null,
    Guid? MarcaId = null, bool? Ativo = true, int Pagina = 1, int TamanhoPagina = 20)
    : IRequest<ListaPaginadaDto<ProdutoDto>>;

public class ListarProdutosHandler(IProdutoRepository repo)
    : IRequestHandler<ListarProdutosQuery, ListaPaginadaDto<ProdutoDto>>
{
    public async Task<ListaPaginadaDto<ProdutoDto>> Handle(ListarProdutosQuery q, CancellationToken ct)
    {
        var produtos = await repo.PesquisarAsync(q.EmpresaId, q.Termo, q.CategoriaId, q.MarcaId, q.Ativo, q.Pagina, q.TamanhoPagina, ct);
        var total = await repo.ContarAsync(q.EmpresaId, q.Termo, q.CategoriaId, q.Ativo, ct);
        var (unidades, categorias, marcas) = await repo.ObterLookupsAsync(q.EmpresaId, ct);

        var dtos = produtos.Select(p => new ProdutoDto(
            p.Id, p.Codigo, p.CodigoBarras, p.Descricao, p.DescricaoComplementar,
            p.CategoriaId, categorias.GetValueOrDefault(p.CategoriaId, ""),
            p.MarcaId, marcas.GetValueOrDefault(p.MarcaId, ""),
            p.UnidadeMedidaId, unidades.GetValueOrDefault(p.UnidadeMedidaId, ""),
            p.CustoUnitario, p.PrecoVenda, p.PrecoAtacado,
            p.Markup, p.MargemLucro,
            p.EstoqueAtual, p.EstoqueMinimo,
            p.Ncm, p.Cest, p.ControlarLote, p.ControlarValidade,
            p.ProdutoBalanca, p.CodigoPlu, p.Ativo, p.CriadoEm,
            p.Referencia, p.PrecoFornecedor, p.MarkupMinimo, p.PrecoMinimo, p.MarkupAtacado,
            p.VendidoFracionado, p.EtiquetaDesatualizada,
            p.FornecedorPrincipalId, p.ValidadeEmDias
        )).ToList();

        return new ListaPaginadaDto<ProdutoDto>(dtos, total, q.Pagina, q.TamanhoPagina);
    }
}
