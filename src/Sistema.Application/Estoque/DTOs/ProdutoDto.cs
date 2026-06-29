namespace Sistema.Application.Estoque.DTOs;

public record ProdutoDto(
    Guid Id, string Codigo, string? CodigoBarras, string Descricao,
    string? DescricaoComplementar,
    Guid CategoriaId, string CategoriaNome, Guid MarcaId, string MarcaNome,
    Guid UnidadeMedidaId, string UnidadeSigla,
    decimal CustoUnitario, decimal PrecoVenda, decimal? PrecoAtacado,
    decimal Markup, decimal MargemLucro,
    decimal EstoqueAtual, decimal EstoqueMinimo,
    string? Ncm, string? Cest, bool ControlarLote, bool ControlarValidade,
    bool ProdutoBalanca, int? CodigoPlu, bool Ativo, DateTime CriadoEm);

public record ListaPaginadaDto<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int TamanhoPagina)
{
    public int TotalPaginas => (int)Math.Ceiling((double)Total / TamanhoPagina);
}
