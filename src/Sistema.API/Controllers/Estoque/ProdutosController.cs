using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Estoque.Commands;
using Sistema.Application.Estoque.Queries;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;
using System.Net.Mime;
using SixLabors.ImageSharp;

namespace Sistema.API.Controllers.Estoque;

[ApiController]
[Route("api/produtos")]
[Authorize]
public class ProdutosController(IMediator mediator, SistemaDbContext db, IUnitOfWork uow,
    Sistema.Infrastructure.Services.GeminiImageService gemini,
    Sistema.Infrastructure.Services.OpenAiTextService openai,
    Sistema.Infrastructure.Services.ImagemProdutoService imagemProduto,
    Sistema.Infrastructure.Services.SiteSyncService siteSync) : ControllerBase
{
    /// <summary>Sincroniza os produtos por kg (balança) com o site público (ecogranel.com.br).</summary>
    [HttpPost("sincronizar-site")]
    public async Task<IActionResult> SincronizarSite([FromQuery] Guid empresaId, CancellationToken ct)
    {
        if (!siteSync.Configurado)
            return StatusCode(503, new { mensagem = "Sincronização com o site não configurada no servidor (Site:SyncUrl)." });

        var lista = await MontarProdutosParaSiteAsync(empresaId, somenteBalanca: true, umId: null, ct);
        if (lista.Count == 0)
            return Ok(new { ok = true, qtd = 0, mensagem = "Nenhum produto por kg (balança) para sincronizar." });

        var (ok, qtd, msg) = await siteSync.EnviarAsync(lista, ct);
        return ok ? Ok(new { ok, qtd, mensagem = msg }) : StatusCode(502, new { ok, mensagem = msg });
    }

    // Monta os produtos no formato que o site espera (nome, descrição, foto, categoria,
    // marca e tabela nutricional — sem preço). `umId` restringe a um único produto.
    private async Task<IReadOnlyList<object>> MontarProdutosParaSiteAsync(
        Guid empresaId, bool somenteBalanca, Guid? umId, CancellationToken ct)
    {
        var q = db.Produtos.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Ativo);
        if (somenteBalanca) q = q.Where(p => p.ProdutoBalanca);
        if (umId.HasValue) q = q.Where(p => p.Id == umId.Value);
        var produtos = await q.ToListAsync(ct);
        if (produtos.Count == 0) return System.Array.Empty<object>();

        var catIds = produtos.Select(p => p.CategoriaId).Distinct().ToList();
        var marcaIds = produtos.Select(p => p.MarcaId).Distinct().ToList();
        var ids = produtos.Select(p => p.Id).ToList();
        var cats = await db.Categorias.AsNoTracking().Where(c => catIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, ct);
        var marcas = await db.Marcas.AsNoTracking().Where(m => marcaIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Nome, ct);
        var nuts = await db.TabelasNutricionais.AsNoTracking().Where(n => ids.Contains(n.ProdutoId))
            .ToDictionaryAsync(n => n.ProdutoId, ct);
        var baseUrl = siteSync.BaseImagens.TrimEnd('/');

        return produtos.Select(p => (object)new
        {
            slug = Sistema.Infrastructure.Services.SiteSyncService.Slugify(p.Descricao),
            codigo = p.Codigo,
            nome = p.Descricao,
            descricao = p.DescricaoComplementar,
            imagemUrl = string.IsNullOrEmpty(p.ImagemUrl) ? null : baseUrl + p.ImagemUrl,
            categoria = cats.GetValueOrDefault(p.CategoriaId),
            marca = marcas.GetValueOrDefault(p.MarcaId),
            ativo = p.Ativo,
            nutricional = nuts.TryGetValue(p.Id, out var n) ? new
            {
                n.Porcao, n.Calorias, n.Proteinas, n.CarboidratosTotais, n.GordurasTotais,
                n.GordurasSaturadas, n.GordurasTrans, n.Sodio, n.FibrasDieteticas, n.Acucares,
                n.VitaminaA, n.VitaminaC, n.VitaminaD, n.Calcio, n.Ferro, n.Magnesio, n.Zinco,
                n.Ingredientes, n.Alergenicos, n.ModoConservacao, n.InformacoesAdicionais
            } : null,
        }).ToList();
    }

    /// <summary>Busca a imagem do produto pelo código de barras (Open Food Facts) e associa ao produto.</summary>
    [HttpPost("{id:guid}/imagem-codigo-barras")]
    public async Task<IActionResult> ImagemPorCodigoBarras(Guid id,
        [FromBody] ImagemPorCodigoBarrasRequest req, CancellationToken ct)
    {
        var produto = await db.Produtos.FindAsync([id], ct);
        if (produto is null) return NotFound(new { mensagem = "Produto não encontrado." });

        var codigo = req.CodigoBarras ?? produto.CodigoBarras;
        if (string.IsNullOrWhiteSpace(codigo))
            return BadRequest(new { mensagem = "Produto sem código de barras para a busca." });

        var achado = await imagemProduto.BuscarAsync(codigo, ct);
        if (achado is null)
            return NotFound(new { mensagem = "Nenhuma imagem encontrada para este código de barras." });

        var (bytes, mime, _) = achado.Value;
        var ext = mime switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };

        var dir = Path.Combine("wwwroot", "uploads", "produtos");
        Directory.CreateDirectory(dir);

        // Remove imagem anterior (qualquer extensão) para não deixar órfã.
        if (!string.IsNullOrEmpty(produto.ImagemUrl))
        {
            var old = Path.Combine("wwwroot", produto.ImagemUrl.TrimStart('/'));
            if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
        }

        var nomeArquivo = $"{id}{ext}";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, nomeArquivo), bytes, ct);

        var url = $"/uploads/produtos/{nomeArquivo}";
        produto.EditarInfoAdicional(url, produto.Marcador, produto.Tags, produto.InformacaoAdicional);
        db.Produtos.Update(produto);
        await uow.SalvarAsync(ct);

        return Ok(new { url });
    }

    /// <summary>
    /// Feed CSV do catálogo (formato Meta Commerce) — a Meta puxa por agendamento
    /// como "Arquivo de dados". Público (sem login) para a Meta acessar. Só produtos
    /// ativos, com preço e COM foto (a Meta exige image_link).
    /// </summary>
    [HttpGet("feed-catalogo")]
    [AllowAnonymous]
    public async Task<IActionResult> FeedCatalogo([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo && p.PrecoVenda > 0
                     && p.ImagemUrl != null && p.ImagemUrl != "")
            .ToListAsync(ct);

        const string baseImg = "https://sistema.ecogranel.com.br";
        static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("id,title,description,availability,condition,price,link,image_link,brand");
        foreach (var p in produtos)
        {
            var slug = Sistema.Infrastructure.Services.SiteSyncService.Slugify(p.Descricao);
            var preco = p.PrecoVenda.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " BRL";
            sb.Append(Csv(p.Codigo)).Append(',')
              .Append(Csv(p.Descricao.Length > 150 ? p.Descricao[..150] : p.Descricao)).Append(',')
              .Append(Csv(string.IsNullOrWhiteSpace(p.DescricaoComplementar) ? p.Descricao : p.DescricaoComplementar)).Append(',')
              .Append("\"in stock\",\"new\",")
              .Append(Csv(preco)).Append(',')
              .Append(Csv($"https://ecogranel.com.br/produtos/produto.php?p={slug}")).Append(',')
              .Append(Csv(baseImg + p.ImagemUrl)).Append(',')
              .Append("\"EcoGranel\"").AppendLine();
        }

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", "catalogo-ecogranel.csv");
    }

    /// <summary>
    /// Devolve a imagem do produto convertida em JPEG. Necessário para o cabeçalho de imagem
    /// dos templates do WhatsApp, que aceita apenas JPEG/PNG (as fotos são salvas em .webp).
    /// Público (sem login) para a Meta conseguir baixar a imagem.
    /// URL estável: /api/produtos/{id}/imagem.jpg
    /// </summary>
    [HttpGet("{id:guid}/imagem.jpg")]
    [AllowAnonymous]
    public async Task<IActionResult> ImagemJpeg(Guid id, CancellationToken ct)
    {
        var produto = await db.Produtos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (produto is null || string.IsNullOrEmpty(produto.ImagemUrl))
            return NotFound();

        var caminho = Path.Combine("wwwroot", produto.ImagemUrl.TrimStart('/'));
        if (!System.IO.File.Exists(caminho))
            return NotFound();

        // Se já for JPEG, devolve direto; senão converte (webp/png → jpeg).
        if (caminho.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            caminho.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return PhysicalFile(Path.GetFullPath(caminho), "image/jpeg");
        }

        using var imagem = await SixLabors.ImageSharp.Image.LoadAsync(caminho, ct);
        var ms = new MemoryStream();
        await imagem.SaveAsJpegAsync(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 }, ct);
        ms.Position = 0;
        return File(ms, "image/jpeg");
    }

    /// <summary>Sugere a descrição complementar (benefícios) de um produto usando IA.
    /// Prefere a OpenAI (ChatGPT); usa o Gemini como alternativa se a OpenAI não estiver configurada.</summary>
    [HttpPost("sugerir-descricao")]
    public async Task<IActionResult> SugerirDescricao([FromBody] SugerirDescricaoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            return BadRequest(new { mensagem = "Informe o nome do produto." });
        if (!openai.Configurado && !gemini.Configurado)
            return StatusCode(503, new { mensagem = "IA não configurada no servidor. Defina OpenAI:ApiKey (ou Gemini:ApiKey)." });

        var prompt =
            $"Você é um especialista em produtos naturais e saudáveis de uma loja brasileira. " +
            $"Escreva uma descrição complementar curta (2 a 4 frases, em português do Brasil) destacando os " +
            $"principais benefícios e sugestões de consumo do produto \"{req.Nome}\". " +
            $"Escreva em tom informativo e comercial, sem inventar dados nutricionais específicos nem fazer " +
            $"promessas de cura. Responda apenas com o texto, sem título, sem markdown e sem aspas.";

        try
        {
            var texto = openai.Configurado
                ? await openai.GerarTextoAsync(prompt, ct)
                : await gemini.GerarTextoAsync(prompt, ct);
            return Ok(new { texto });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { mensagem = ex.Message });
        }
    }

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
        var novo = await db.Produtos.AsNoTracking()
            .Where(p => p.Id == id).Select(p => new { p.Codigo, p.ProdutoBalanca }).FirstOrDefaultAsync(ct);

        // Produto por kg novo → sincroniza com o site. Os dados são montados agora
        // (db ainda vivo) e só o POST HTTP fica em segundo plano (não bloqueia a resposta).
        if (novo?.ProdutoBalanca == true && siteSync.Configurado)
        {
            var lista = await MontarProdutosParaSiteAsync(cmd.EmpresaId, somenteBalanca: false, umId: id, ct);
            if (lista.Count > 0) _ = siteSync.EnviarAsync(lista);
        }

        return CreatedAtAction(nameof(ObterPorId), new { id }, new { id, codigo = novo?.Codigo });
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

        // Código interno: só altera se veio preenchido e diferente, barrando duplicidade.
        var novoCodigo = req.Codigo?.Trim();
        if (!string.IsNullOrWhiteSpace(novoCodigo) && novoCodigo != produto.Codigo)
        {
            var duplicado = await db.Produtos.AsNoTracking().AnyAsync(
                p => p.EmpresaId == produto.EmpresaId && p.Id != id && p.Codigo == novoCodigo, ct);
            if (duplicado)
                return Conflict(new { mensagem = $"Já existe um produto com o código {novoCodigo}." });
            produto.EditarCodigo(novoCodigo);
        }

        produto.AtualizarCodigoBarras(req.CodigoBarras);

        produto.EditarPrecos(req.PrecoFornecedor, req.CustoUnitario,
            req.MarkupMinimo, req.PrecoMinimo,
            req.PrecoVenda, req.PrecoAtacado, req.MarkupAtacado);

        produto.EditarFiscal(req.Ncm, req.Cest, req.CstIcms, req.CsosnIcms,
            req.CstPisCofins, req.AliquotaIcms, req.AliquotaPis, req.AliquotaCofins,
            req.Cfop, req.Origem ?? "0", req.CodigoFci);

        // A imagem é gerenciada pelos endpoints próprios (upload/remover) — a edição
        // geral NÃO deve tocar nela (senão um form desatualizado apaga a foto).
        produto.EditarInfoAdicional(produto.ImagemUrl, req.Marcador, req.Tags, req.InformacaoAdicional);

        // Unidade pesável (KG) → sempre marcado para a balança.
        var pesavel = await db.UnidadesMedida.AsNoTracking()
            .AnyAsync(u => u.Id == req.UnidadeMedidaId && (u.Pesavel || u.Sigla == "KG"), ct);
        if (pesavel) produto.MarcarComoBalanca(req.CodigoPlu);

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

    // ── Produtos duplicados: detectar e unificar ──────────────────────────

    /// <summary>Lista grupos de produtos duplicados (mesma descrição ou mesmo código de barras).</summary>
    [HttpGet("duplicados")]
    public async Task<IActionResult> Duplicados([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId)
            .Select(p => new ProdutoDupDto(p.Id, p.Codigo, p.Descricao, p.CodigoBarras,
                p.EstoqueAtual, p.PrecoVenda, p.Ativo, p.CriadoEm))
            .ToListAsync(ct);

        var grupos = new List<object>();
        var jaAgrupados = new HashSet<Guid>();

        // Por descrição idêntica (normalizada)
        foreach (var g in produtos.GroupBy(p => p.Descricao.Trim().ToUpperInvariant()).Where(g => g.Count() > 1))
        {
            var itens = g.OrderBy(p => p.CriadoEm).ToList();
            foreach (var p in itens) jaAgrupados.Add(p.Id);
            grupos.Add(new { chave = "Descrição: " + itens[0].Descricao, produtos = itens });
        }

        // Por código de barras (que não caíram no grupo de descrição)
        foreach (var g in produtos
            .Where(p => !string.IsNullOrWhiteSpace(p.CodigoBarras) && !jaAgrupados.Contains(p.Id))
            .GroupBy(p => p.CodigoBarras).Where(g => g.Count() > 1))
        {
            var itens = g.OrderBy(p => p.CriadoEm).ToList();
            foreach (var p in itens) jaAgrupados.Add(p.Id);
            grupos.Add(new { chave = "Cód. barras: " + g.Key, produtos = itens });
        }

        // Por descrição-base similar: remove sufixos de embalagem/peso e conteúdo entre
        // parênteses. Ex.: "COLORAU FORTE" ≈ "COLORAU FORTE (NACIONAL)-5KG". Como a
        // unificação é sempre confirmada manualmente, são sugestões (candidatos).
        foreach (var g in produtos
            .Where(p => !jaAgrupados.Contains(p.Id))
            .GroupBy(p => NormalizarBase(p.Descricao))
            .Where(g => g.Key.Length >= 3 && g.Count() > 1))
        {
            var itens = g.OrderBy(p => p.CriadoEm).ToList();
            foreach (var p in itens) jaAgrupados.Add(p.Id);
            grupos.Add(new { chave = "Similar: " + itens[0].Descricao, similar = true, produtos = itens });
        }

        return Ok(grupos);
    }

    /// <summary>
    /// Produtos parecidos com uma descrição — usado na escrituração de NF-e, onde a
    /// descrição da nota raramente é igual à do cadastro
    /// ("Castanha de caju sem sal" × "CASTANHA DE CAJU W1 240 TORRADA SEM SAL ...").
    /// Compara por palavras, então a ordem e os complementos não atrapalham.
    /// </summary>
    [HttpGet("similares")]
    public async Task<IActionResult> Similares(
        [FromQuery] Guid empresaId, [FromQuery] string? descricao,
        [FromQuery] int limite = 5, CancellationToken ct = default)
    {
        var alvo = TokensBase(descricao ?? string.Empty);
        if (alvo.Count == 0) return Ok(Array.Empty<object>());

        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Ativo)
            .Select(p => new
            {
                p.Id, p.Codigo, p.Descricao, p.CodigoBarras, p.UnidadeMedidaId,
                p.PrecoVenda, p.CustoUnitario, p.EstoqueAtual, p.Markup,
            })
            .ToListAsync(ct);

        var achados = produtos
            .Select(p => new { p, score = Similaridade(alvo, TokensBase(p.Descricao)) })
            .Where(x => x.score >= 0.5m)          // abaixo disso vira ruído
            .OrderByDescending(x => x.score).ThenBy(x => x.p.Descricao.Length)
            .Take(Math.Clamp(limite, 1, 20))
            .Select(x => new
            {
                x.p.Id, x.p.Codigo, x.p.Descricao, x.p.CodigoBarras, x.p.UnidadeMedidaId,
                x.p.PrecoVenda, x.p.CustoUnitario, x.p.EstoqueAtual, x.p.Markup,
                Score = Math.Round(x.score * 100, 0),
            })
            .ToList();

        return Ok(achados);
    }

    /// <summary>Palavras relevantes da descrição (sem conectivos, embalagem ou pontuação).</summary>
    private static List<string> TokensBase(string descricao) =>
        NormalizarBase(descricao)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !_conectivos.Contains(t))
            .Distinct()
            .ToList();

    // "SEM"/"COM" ficam de fora da lista: distinguem produtos (sem sal × com sal)
    private static readonly HashSet<string> _conectivos =
        new(StringComparer.Ordinal) { "DE", "DA", "DO", "DAS", "DOS", "E", "EM", "NO", "NA", "PARA" };

    /// <summary>
    /// Semelhança entre duas listas de palavras: quanto da menor está contida na
    /// maior. Assim "castanha caju sem sal" casa 100% com o nome longo do cadastro,
    /// sem que os complementos (marca, peso) derrubem a nota.
    /// </summary>
    private static decimal Similaridade(List<string> a, List<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0m;
        var comuns = a.Count(t => b.Contains(t));
        return (decimal)comuns / Math.Min(a.Count, b.Count);
    }

    private static readonly System.Text.RegularExpressions.Regex _reParenteses =
        new(@"\(.*?\)", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex _reEmbalagem =
        new(@"[-–]?\s*\d+[.,]?\d*\s*(KG|KGS|G|GR|GRS|MG|ML|L|LT|LTS|UN|UND|CX|PCT|PC|PACOTE|FARDO|SACO)\b",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex _reNaoAlfa =
        new(@"[^A-Z0-9 ]", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Descrição-base para detecção de similares: remove parênteses, sufixos de
    /// peso/embalagem e pontuação, para colapsar variações do mesmo produto.
    /// </summary>
    private static string NormalizarBase(string descricao)
    {
        var s = (descricao ?? string.Empty).ToUpperInvariant();
        s = _reParenteses.Replace(s, " ");
        s = _reEmbalagem.Replace(s, " ");
        s = _reNaoAlfa.Replace(s, " ");
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private record ProdutoDupDto(Guid Id, string Codigo, string Descricao, string? CodigoBarras,
        decimal EstoqueAtual, decimal PrecoVenda, bool Ativo, DateTime CriadoEm);

    /// <summary>
    /// Unifica produtos duplicados: reaponta todas as referências (movimentações, vendas,
    /// entradas, lotes, etc.) das origens para o destino, soma o estoque e remove as origens.
    /// </summary>
    [HttpPost("unificar")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Unificar([FromBody] UnificarProdutosRequest req, CancellationToken ct)
    {
        if (req.OrigemIds is null || req.OrigemIds.Count == 0)
            return BadRequest(new { mensagem = "Informe os produtos a unificar." });
        if (req.OrigemIds.Contains(req.DestinoId))
            return BadRequest(new { mensagem = "O produto mantido não pode estar na lista de duplicados." });

        var destino = await db.Produtos.FirstOrDefaultAsync(p => p.Id == req.DestinoId, ct)
            ?? throw new KeyNotFoundException("Produto de destino não encontrado.");
        var origens = await db.Produtos.Where(p => req.OrigemIds.Contains(p.Id)).ToListAsync(ct);
        if (origens.Count == 0) return BadRequest(new { mensagem = "Nenhum produto de origem encontrado." });

        // Tabelas transacionais (muitas por produto) → só reaponta o ProdutoId
        var tabelas = new[]
        {
            "AlertasValidade", "ItensCatalogo", "ItensDevolucoesVenda", "ItensEntradaNFe",
            "ItensNotaFiscal", "ItensPedidoCompra", "ItensPedidoWhatsApp", "ItensVenda",
            "Lotes", "MovimentacoesEstoque", "ProdutosEmbalagem", "ReceitasProduto", "SugestoesProduto",
        };

        var idsOrigem = origens.Select(o => o.Id).ToArray();
        var idsCsv = string.Join(",", idsOrigem.Select(i => $"'{i}'"));
        var somaEstoque = origens.Sum(o => o.EstoqueAtual);

        // Pré-checa 1-por-produto (nutricional/QR) fora da transação
        var temNutriDestino = await db.TabelasNutricionais.AnyAsync(n => n.ProdutoId == req.DestinoId, ct);
        var temQrDestino = await db.QrCodesProduto.AnyAsync(q => q.ProdutoId == req.DestinoId, ct);

        // O DbContext usa retry (EnableRetryOnFailure), que exige que a transação
        // seja executada como unidade retriável via execution strategy.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var tx = await db.Database.BeginTransactionAsync(ct);

            foreach (var t in tabelas)
                await db.Database.ExecuteSqlRawAsync(
                    $"UPDATE [{t}] SET ProdutoId = {{0}} WHERE ProdutoId IN ({idsCsv})", new object[] { req.DestinoId }, ct);

            // Nutricional e QR Code: 1 por produto → mantém o do destino; move do origem só se destino não tiver
            foreach (var (tabela, temNoDestino) in new[]
            {
                ("TabelasNutricionais", temNutriDestino),
                ("QrCodesProduto",      temQrDestino),
            })
            {
                if (temNoDestino)
                    await db.Database.ExecuteSqlRawAsync($"DELETE FROM [{tabela}] WHERE ProdutoId IN ({idsCsv})", ct);
                else
                {
                    // Move só o primeiro; remove os demais para não violar unicidade
                    await db.Database.ExecuteSqlRawAsync(
                        $"UPDATE TOP (1) [{tabela}] SET ProdutoId = {{0}} WHERE ProdutoId IN ({idsCsv})", new object[] { req.DestinoId }, ct);
                    await db.Database.ExecuteSqlRawAsync($"DELETE FROM [{tabela}] WHERE ProdutoId IN ({idsCsv})", ct);
                }
            }

            // Soma o estoque das origens no destino
            if (somaEstoque != 0)
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE Produtos SET EstoqueAtual = EstoqueAtual + {0} WHERE Id = {1}",
                    new object[] { somaEstoque, req.DestinoId }, ct);

            // Remove os produtos de origem
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM Produtos WHERE Id IN ({idsCsv})", ct);

            await tx.CommitAsync(ct);
        });

        return Ok(new { unificados = origens.Count, destino = req.DestinoId, estoqueSomado = somaEstoque });
    }

    /// <summary>Marca as etiquetas dos produtos informados como impressas/atualizadas (limpa o alerta de reimpressão).</summary>
    [HttpPost("etiquetas-impressas")]
    public async Task<IActionResult> MarcarEtiquetasImpressas([FromBody] EtiquetasImpressasRequest req, CancellationToken ct)
    {
        if (req.Ids is null || req.Ids.Count == 0) return Ok(new { atualizados = 0 });
        var produtos = await db.Produtos.Where(p => req.Ids.Contains(p.Id)).ToListAsync(ct);
        foreach (var p in produtos) p.MarcarEtiquetaImpressa();
        await uow.SalvarAsync(ct);
        return Ok(new { atualizados = produtos.Count });
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

public record EtiquetasImpressasRequest(List<Guid> Ids);

public record UnificarProdutosRequest(Guid DestinoId, List<Guid> OrigemIds);

public record SugerirDescricaoRequest(string Nome);

public record ImagemPorCodigoBarrasRequest(string? CodigoBarras);

public record EditarProdutoCompletoRequest(
    // Geral
    string Descricao, string? Referencia, Guid CategoriaId, Guid MarcaId,
    Guid UnidadeMedidaId, Guid? FornecedorPrincipalId,
    string? CodigoBarras = null, string? Codigo = null,
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
