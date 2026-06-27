using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Publico;

/// <summary>Página pública do produto acessada via QR Code na etiqueta. Sem autenticação.</summary>
[ApiController]
[Route("produto")]
[AllowAnonymous]
public class ProdutoPublicoController(
    IQrCodeProdutoRepository qrRepo,
    ITabelaNutricionalRepository nutRepo,
    IReceitaProdutoRepository receitaRepo,
    ISugestaoProdutoRepository sugestaoRepo,
    IProdutoRepository produtoRepo,
    IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{slug}")]
    public async Task<IActionResult> Pagina(string slug, CancellationToken ct)
    {
        var qr = await qrRepo.ObterPorSlugAsync(slug, ct);
        if (qr is null) return NotFound(new { mensagem = "Produto não encontrado." });

        // Registra scan
        qr.RegistrarScan();
        qrRepo.Atualizar(qr);
        await uow.SalvarAsync(ct);

        var produto = await produtoRepo.ObterPorIdAsync(qr.ProdutoId, ct);
        if (produto is null) return NotFound();

        var nutricional = await nutRepo.ObterPorProdutoAsync(qr.ProdutoId, ct);
        var receitas = await receitaRepo.ListarPorProdutoAsync(qr.ProdutoId, ct);
        var sugestoes = await sugestaoRepo.ListarPorProdutoAsync(qr.ProdutoId, ct);

        return Ok(new
        {
            produto = new
            {
                produto.Id, produto.Descricao, produto.Codigo,
                produto.CodigoBarras, produto.PrecoVenda
            },
            tabelaNutricional = nutricional is null ? null : new
            {
                nutricional.Porcao, nutricional.Calorias, nutricional.Proteinas,
                nutricional.CarboidratosTotais, nutricional.GordurasTotais,
                nutricional.GordurasSaturadas, nutricional.GordurasTrans,
                nutricional.Sodio, nutricional.FibrasDieteticas, nutricional.Acucares,
                nutricional.VitaminaA, nutricional.VitaminaC, nutricional.VitaminaD,
                nutricional.Calcio, nutricional.Ferro, nutricional.Magnesio, nutricional.Zinco,
                nutricional.Ingredientes, nutricional.Alergenicos,
                nutricional.ModoConservacao, nutricional.InformacoesAdicionais
            },
            receitas = receitas.Select(r => new
            {
                r.Titulo, r.Descricao, r.Ingredientes, r.ModoPreparo,
                r.Dicas, r.TempoPreparoMinutos, r.Porcoes, r.UrlFoto, r.Ordem
            }),
            sugestoes = sugestoes.Select(s => new
            {
                s.Titulo, s.Descricao, s.Tipo, s.ProdutoRelacionadoId, s.Ordem
            }),
            qrCode = new { qr.Slug, qr.UrlPublica, qr.TotalScans }
        });
    }
}
