using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Estoque.Commands;
using Sistema.Application.Estoque.Queries;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;
using System.Net.Mime;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/produtos")]
[Authorize]
public class ProdutosController(IMediator mediator, SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid empresaId,
        [FromQuery] string? termo,
        [FromQuery] Guid? categoriaId,
        [FromQuery] Guid? marcaId,
        [FromQuery] bool? ativo = true,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default)
    {
        var resultado = await mediator.Send(
            new ListarProdutosQuery(empresaId, termo, categoriaId, marcaId, ativo, pagina, tamanhoPagina), ct);
        return Ok(resultado);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var p = await db.Produtos.AsNoTracking()
            .Include(p => p.Embalagens.Where(e => e.Ativo))
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (p is null) return NotFound();

        var nutricional = await db.TabelasNutricionais.AsNoTracking()
            .FirstOrDefaultAsync(n => n.ProdutoId == id, ct);

        return Ok(new
        {
            p.Id, p.EmpresaId, p.Codigo, p.Referencia, p.Descricao, p.DescricaoComplementar,
            p.CategoriaId, p.MarcaId, p.UnidadeMedidaId, p.FornecedorPrincipalId,
            p.TipoVariacao, p.CodigoBarras,
            p.ProdutoBalanca, p.CodigoPlu, p.OcultarNasVendas, p.RequisitarVendedor,
            p.VendidoFracionado, p.Ativo,
            p.ControlarLote, p.ControlarValidade, p.ValidadeEmDias,
            p.Ncm, p.Cest, p.CstIcms, p.CsosnIcms, p.CstPisCofins,
            p.AliquotaIcms, p.AliquotaPis, p.AliquotaCofins, p.Cfop, p.Origem, p.CodigoFci,
            p.PrecoFornecedor, p.CustoUnitario, p.MarkupMinimo, p.PrecoMinimo,
            p.PrecoVenda, p.PrecoAtacado, p.MarkupAtacado, p.Markup, p.MargemLucro,
            p.EstoqueAtual, p.EstoqueMinimo, p.EstoqueMaximo,
            p.ImagemUrl, p.FichaTecnicaUrl, p.Marcador, p.Tags, p.InformacaoAdicional,
            embalagens = p.Embalagens.Select(e => new {
                e.Id, e.UnidadeMedidaId, e.Descricao, e.Multiplicador, e.CodigoBarras, e.PrecoVenda
            }),
            nutricional,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarProdutoCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] EditarProdutoCompletoRequest req,
        CancellationToken ct)
    {
        var produto = await db.Produtos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        produto.EditarGeral(req.Descricao, req.Referencia, req.CategoriaId, req.MarcaId,
            req.UnidadeMedidaId, req.FornecedorPrincipalId, req.TipoVariacao,
            req.ProdutoBalanca, req.CodigoPlu, req.OcultarNasVendas,
            req.RequisitarVendedor, req.VendidoFracionado, req.Ativo,
            req.ControlarLote, req.ControlarValidade, req.ValidadeEmDias,
            req.DescricaoComplementar);

        produto.EditarPrecos(req.PrecoFornecedor, req.CustoUnitario,
            req.MarkupMinimo, req.PrecoMinimo,
            req.PrecoVenda, req.PrecoAtacado, req.MarkupAtacado);

        produto.EditarFiscal(req.Ncm, req.Cest, req.CstIcms, req.CsosnIcms,
            req.CstPisCofins, req.AliquotaIcms, req.AliquotaPis, req.AliquotaCofins,
            req.Cfop, req.Origem ?? "0", req.CodigoFci);

        produto.EditarInfoAdicional(req.ImagemUrl, req.Marcador, req.Tags, req.InformacaoAdicional);

        db.Produtos.Update(produto);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // Mantém compatibilidade com a versão anterior (PATCH de preço)
    [HttpPatch("{id:guid}/preco")]
    public async Task<IActionResult> AtualizarPreco(Guid id, [FromBody] AtualizarPrecoRequest req,
        [FromQuery] Guid empresaId, CancellationToken ct)
    {
        await mediator.Send(new AtualizarPrecoCommand(empresaId, id, req.NovoCusto, req.NovoPreco), ct);
        return NoContent();
    }

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar(
        [FromQuery] Guid empresaId, [FromQuery] string? q,
        CancellationToken ct = default)
    {
        var resultado = await mediator.Send(
            new ListarProdutosQuery(empresaId, q, null, null, true, 1, 20), ct);
        return Ok(resultado.Itens);
    }

    // ── Imagem do produto ─────────────────────────────────────────────
    [HttpPost("{id:guid}/imagem")]
    [RequestSizeLimit(5_000_000)] // 5 MB
    public async Task<IActionResult> UploadImagem(Guid id, IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0) return BadRequest("Nenhum arquivo enviado.");

        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest("Apenas imagens JPG, PNG ou WebP são aceitas.");

        var produto = await db.Produtos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        var dir = Path.Combine("wwwroot", "uploads", "produtos");
        Directory.CreateDirectory(dir);

        if (!string.IsNullOrEmpty(produto.ImagemUrl))
        {
            var old = Path.Combine("wwwroot", produto.ImagemUrl.TrimStart('/'));
            if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
        }

        var nomeArquivo = $"{id}{ext}";
        var caminho = Path.Combine(dir, nomeArquivo);
        using (var stream = System.IO.File.Create(caminho))
            await arquivo.CopyToAsync(stream, ct);

        var url = $"/uploads/produtos/{nomeArquivo}";
        produto.EditarInfoAdicional(url, produto.Marcador, produto.Tags, produto.InformacaoAdicional);
        db.Produtos.Update(produto);
        await uow.SalvarAsync(ct);

        return Ok(new { url });
    }

    [HttpDelete("{id:guid}/imagem")]
    public async Task<IActionResult> ExcluirImagem(Guid id, CancellationToken ct)
    {
        var produto = await db.Produtos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        if (!string.IsNullOrEmpty(produto.ImagemUrl))
        {
            var caminho = Path.Combine("wwwroot", produto.ImagemUrl.TrimStart('/'));
            if (System.IO.File.Exists(caminho)) System.IO.File.Delete(caminho);
        }

        produto.EditarInfoAdicional(null, produto.Marcador, produto.Tags, produto.InformacaoAdicional);
        db.Produtos.Update(produto);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // ── Ficha Técnica (PDF) ────────────────────────────────────────────
    [HttpPost("{id:guid}/ficha-tecnica")]
    [RequestSizeLimit(20_000_000)] // 20 MB
    public async Task<IActionResult> UploadFichaTecnica(Guid id, IFormFile arquivo,
        CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest("Nenhum arquivo enviado.");

        if (!arquivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !arquivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Apenas arquivos PDF são aceitos.");

        var produto = await db.Produtos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        var dir = Path.Combine("wwwroot", "uploads", "fichas-tecnicas");
        Directory.CreateDirectory(dir);

        // Remove ficha anterior se existir
        if (!string.IsNullOrEmpty(produto.FichaTecnicaUrl))
        {
            var old = Path.Combine("wwwroot", produto.FichaTecnicaUrl.TrimStart('/'));
            if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
        }

        var nomeArquivo = $"{id}.pdf";
        var caminho = Path.Combine(dir, nomeArquivo);
        using (var stream = System.IO.File.Create(caminho))
            await arquivo.CopyToAsync(stream, ct);

        var url = $"/uploads/fichas-tecnicas/{nomeArquivo}";
        produto.DefinirFichaTecnica(url);
        db.Produtos.Update(produto);
        await uow.SalvarAsync(ct);

        return Ok(new { url });
    }

    [HttpDelete("{id:guid}/ficha-tecnica")]
    public async Task<IActionResult> ExcluirFichaTecnica(Guid id, CancellationToken ct)
    {
        var produto = await db.Produtos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        if (!string.IsNullOrEmpty(produto.FichaTecnicaUrl))
        {
            var caminho = Path.Combine("wwwroot", produto.FichaTecnicaUrl.TrimStart('/'));
            if (System.IO.File.Exists(caminho)) System.IO.File.Delete(caminho);
        }

        produto.DefinirFichaTecnica(null);
        db.Produtos.Update(produto);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var produto = await db.Produtos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");
        var emNFe = await db.Set<Sistema.Domain.Fiscal.Entities.ItemEntradaNFe>()
            .AnyAsync(i => i.ProdutoId == id, ct);
        var emVenda = await db.ItensVenda.AnyAsync(i => i.ProdutoId == id, ct);
        var emMovimento = await db.MovimentacoesEstoque.AnyAsync(m => m.ProdutoId == id, ct);
        if (emNFe || emVenda || emMovimento)
            return BadRequest(new { mensagem = "Produto possui movimentações (vendas, entradas ou estoque) e não pode ser excluído. Inative-o." });
        db.Produtos.Remove(produto);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/inativar")]
    public async Task<IActionResult> Inativar(Guid id, CancellationToken ct)
        => await DefinirAtivo(id, false, ct);

    [HttpPatch("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
        => await DefinirAtivo(id, true, ct);

    private async Task<IActionResult> DefinirAtivo(Guid id, bool ativo, CancellationToken ct)
    {
        var produto = await db.Produtos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");
        produto.EditarGeral(produto.Descricao, produto.Referencia, produto.CategoriaId, produto.MarcaId,
            produto.UnidadeMedidaId, produto.FornecedorPrincipalId, produto.TipoVariacao,
            produto.ProdutoBalanca, produto.CodigoPlu, produto.OcultarNasVendas,
            produto.RequisitarVendedor, produto.VendidoFracionado, ativo,
            produto.ControlarLote, produto.ControlarValidade, produto.ValidadeEmDias,
            produto.DescricaoComplementar);
        db.Produtos.Update(produto);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/ficha-tecnica")]
    [AllowAnonymous]
    public async Task<IActionResult> BaixarFichaTecnica(Guid id, CancellationToken ct)
    {
        var produto = await db.Produtos.AsNoTracking()
            .Select(p => new { p.Id, p.FichaTecnicaUrl, p.Descricao })
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (produto is null || string.IsNullOrEmpty(produto.FichaTecnicaUrl))
            return NotFound("Ficha técnica não encontrada.");

        var caminho = Path.Combine("wwwroot", produto.FichaTecnicaUrl.TrimStart('/'));
        if (!System.IO.File.Exists(caminho)) return NotFound("Arquivo não encontrado.");

        var bytes = await System.IO.File.ReadAllBytesAsync(caminho, ct);
        var nomeDownload = $"FichaTecnica_{produto.Descricao.Replace(" ", "_")}.pdf";
        return File(bytes, MediaTypeNames.Application.Pdf, nomeDownload);
    }

    [HttpGet("estoque-minimo")]
    public async Task<IActionResult> EstoqueAbaixoMinimo([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var resultado = await mediator.Send(
            new ListarProdutosQuery(empresaId, null, null, null, true, 1, 500), ct);
        return Ok(resultado);
    }

    [HttpPatch("alterar-precos")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> AlterarPrecos(
        [FromQuery] Guid empresaId,
        [FromBody] AlterarPrecosRequest req,
        CancellationToken ct)
    {
        if (req.Itens is null || req.Itens.Count == 0)
            return BadRequest(new { mensagem = "Nenhum item informado." });

        var ids = req.Itens.Select(i => i.ProdutoId).ToList();
        var produtos = await db.Produtos
            .Where(p => p.EmpresaId == empresaId && ids.Contains(p.Id))
            .ToListAsync(ct);

        int atualizados = 0;
        foreach (var item in req.Itens)
        {
            var produto = produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
            if (produto is null) continue;

            var custo = item.NovoCusto ?? produto.CustoUnitario;

            // Se informou markup, recalcula preço de venda pelo custo
            var novoPreco = item.NovoMarkup.HasValue && custo > 0
                ? Math.Round(custo * item.NovoMarkup.Value, 2)
                : item.NovoPrecoVenda ?? produto.PrecoVenda;

            produto.EditarPrecos(
                precoFornecedor: item.NovoPrecoFornecedor ?? produto.PrecoFornecedor,
                custoUnitario: custo,
                markupMinimo: item.NovoMarkupMinimo ?? produto.MarkupMinimo,
                precoMinimo: item.NovoPrecoMinimo ?? produto.PrecoMinimo,
                precoVenda: novoPreco,
                precoAtacado: item.NovoPrecoAtacado ?? produto.PrecoAtacado,
                markupAtacado: item.NovoMarkupAtacado ?? produto.MarkupAtacado);

            atualizados++;
        }

        await uow.SalvarAsync(ct);
        return Ok(new { atualizados });
    }
}

public record AlterarPrecoItemRequest(
    Guid ProdutoId,
    decimal? NovoPrecoVenda,
    decimal? NovoPrecoAtacado,
    decimal? NovoMarkup,
    decimal? NovoCusto,
    decimal? NovoPrecoFornecedor,
    decimal? NovoMarkupMinimo,
    decimal? NovoPrecoMinimo,
    decimal? NovoMarkupAtacado);

public record AlterarPrecosRequest(List<AlterarPrecoItemRequest> Itens);

public record AtualizarPrecoRequest(decimal NovoCusto, decimal NovoPreco);

public record EditarProdutoCompletoRequest(
    // Geral
    string Descricao, string? Referencia, Guid CategoriaId, Guid MarcaId,
    Guid UnidadeMedidaId, Guid? FornecedorPrincipalId,
    string TipoVariacao = "Simples", bool ProdutoBalanca = false,
    int? CodigoPlu = null, bool OcultarNasVendas = false,
    bool RequisitarVendedor = false, bool VendidoFracionado = false,
    bool Ativo = true, bool ControlarLote = false, bool ControlarValidade = false,
    int? ValidadeEmDias = null, string? DescricaoComplementar = null,
    // Preços
    decimal PrecoFornecedor = 0, decimal CustoUnitario = 0,
    decimal MarkupMinimo = 0, decimal PrecoMinimo = 0,
    decimal PrecoVenda = 0, decimal? PrecoAtacado = null, decimal? MarkupAtacado = null,
    // Fiscal
    string? Ncm = null, string? Cest = null, string? CstIcms = null,
    string? CsosnIcms = null, string? CstPisCofins = null,
    decimal AliquotaIcms = 0, decimal AliquotaPis = 0, decimal AliquotaCofins = 0,
    string? Cfop = null, string? Origem = "0", string? CodigoFci = null,
    // Info adicional
    string? ImagemUrl = null, string? Marcador = null,
    string? Tags = null, string? InformacaoAdicional = null);
