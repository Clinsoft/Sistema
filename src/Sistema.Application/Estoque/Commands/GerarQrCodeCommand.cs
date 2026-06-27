using MediatR;
using QRCoder;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Estoque.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using System.Text.RegularExpressions;

namespace Sistema.Application.Estoque.Commands;

public record GerarQrCodeCommand(Guid ProdutoId, string UrlBase) : IRequest<GerarQrCodeResult>;

public record GerarQrCodeResult(string Slug, string UrlPublica, string QrCodeBase64);

public class GerarQrCodeHandler(
    IProdutoRepository produtoRepo,
    IQrCodeProdutoRepository qrRepo,
    IUnitOfWork uow)
    : IRequestHandler<GerarQrCodeCommand, GerarQrCodeResult>
{
    public async Task<GerarQrCodeResult> Handle(GerarQrCodeCommand cmd, CancellationToken ct)
    {
        var produto = await produtoRepo.ObterPorIdAsync(cmd.ProdutoId, ct)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        var slug = GerarSlug(produto.Descricao) + "-" + cmd.ProdutoId.ToString()[..8];
        var qr = await qrRepo.ObterPorProdutoAsync(cmd.ProdutoId, ct);

        string base64;
        using (var gen = new QRCodeGenerator())
        using (var data = gen.CreateQrCode($"{cmd.UrlBase.TrimEnd('/')}/produto/{slug}", QRCodeGenerator.ECCLevel.M))
        using (var code = new PngByteQRCode(data))
        {
            var bytes = code.GetGraphic(10);
            base64 = Convert.ToBase64String(bytes);
        }

        if (qr is null)
        {
            qr = QrCodeProduto.Criar(cmd.ProdutoId, slug, cmd.UrlBase);
            qr.AtualizarQrCode(base64);
            await qrRepo.AdicionarAsync(qr, ct);
        }
        else
        {
            qr.AtualizarQrCode(base64);
            qrRepo.Atualizar(qr);
        }

        await uow.SalvarAsync(ct);
        return new GerarQrCodeResult(qr.Slug, qr.UrlPublica, base64);
    }

    private static string GerarSlug(string texto)
    {
        var s = texto.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        s = Regex.Replace(s, @"\p{Mn}", "");
        s = Regex.Replace(s, @"[^a-z0-9\s-]", "");
        s = Regex.Replace(s, @"\s+", "-");
        return s.Trim('-')[..Math.Min(s.Length, 60)];
    }
}
