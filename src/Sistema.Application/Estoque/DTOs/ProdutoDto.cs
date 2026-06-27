namespace Sistema.Application.Estoque.DTOs;

public record ProdutoDto(
    Guid Id, string Codigo, string? CodigoBarras, string Descricao,
    Guid CategoriaId, string CategoriaNome, Guid MarcaId, string MarcaNome,
    Guid UnidadeMedidaId, string UnidadeSigla,
    decimal CustoUnitario, decimal PrecoVenda, decimal? PrecoAtacado,
    decimal Markup, decimal MargemLucro,
    decimal EstoqueAtual, decimal EstoqueMinimo,
    string? Ncm, string? Cest, bool ControlarLote, bool ControlarValidade,
    bool ProdutoBalanca, bool Ativo, DateTime CriadoEm);

public record ListaPaginadaDto<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int TamanhoPagina)
{
    public int TotalPaginas => (int)Math.Ceiling((double)Total / TamanhoPagina);
}
