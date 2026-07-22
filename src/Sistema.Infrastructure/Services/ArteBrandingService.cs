using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Aplica a identidade visual da marca (logo) sobre uma arte gerada por IA.
/// A logo real é sobreposta ao PNG — a IA cuida das cores/estilo via prompt,
/// mas a logo em si é o arquivo oficial, para não sair distorcida.
/// Caminho da logo: config Branding:LogoPath (padrão wwwroot/brand/logo-ecogranel.png).
/// </summary>
public class ArteBrandingService(IConfiguration config)
{
    private string LogoPath => config["Branding:LogoPath"]
        ?? Path.Combine("wwwroot", "brand", "logo-ecogranel.png");

    public bool LogoDisponivel => File.Exists(LogoPath);

    /// <summary>
    /// Sobrepõe a logo, discreta, no canto superior esquerdo da arte (sem faixa de
    /// fundo). Se a logo não existir, retorna os bytes originais inalterados.
    /// </summary>
    public async Task<byte[]> AplicarLogoAsync(byte[] arte, CancellationToken ct = default)
    {
        if (!LogoDisponivel) return arte;

        using var img = Image.Load<Rgba32>(arte);
        using var logo = Image.Load<Rgba32>(LogoPath);

        // Logo menor (~18% da largura), mantendo proporção.
        var larguraLogo = (int)(img.Width * 0.18);
        var alturaLogo = (int)(logo.Height * (larguraLogo / (double)logo.Width));
        logo.Mutate(x => x.Resize(larguraLogo, alturaLogo));

        // Canto superior esquerdo, com margem — sem faixa de fundo.
        var margem = (int)(img.Width * 0.035);
        img.Mutate(ctx => ctx.DrawImage(logo, new Point(margem, margem), 1f));

        using var ms = new MemoryStream();
        await img.SaveAsPngAsync(ms, ct);
        return ms.ToArray();
    }

    /// <summary>
    /// Sobrepõe a foto real do produto no canto inferior direito da arte, sobre um
    /// cartão branco arredondado para contraste. Aceita qualquer formato lido pelo
    /// ImageSharp (webp/png/jpg). Se a foto não existir, retorna a arte inalterada.
    /// </summary>
    public async Task<byte[]> AplicarFotoProdutoAsync(byte[] arte, string? fotoPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fotoPath) || !File.Exists(fotoPath)) return arte;

        using var img  = Image.Load<Rgba32>(arte);
        using var foto = Image.Load<Rgba32>(fotoPath);

        // Foto ~40% da largura da arte, mantendo proporção.
        var larguraFoto = (int)(img.Width * 0.40);
        var alturaFoto   = (int)(foto.Height * (larguraFoto / (double)foto.Width));
        foto.Mutate(x => x.Resize(larguraFoto, alturaFoto));

        // Cartão branco arredondado atrás da foto, com um respiro de margem.
        var pad     = (int)(img.Width * 0.02);
        var cardW   = larguraFoto + pad * 2;
        var cardH   = alturaFoto + pad * 2;
        var margem  = (int)(img.Width * 0.05);
        var cardX   = img.Width  - cardW - margem;
        var cardY   = img.Height - cardH - margem;

        using var card = new Image<Rgba32>(cardW, cardH, Color.White);

        img.Mutate(ctx =>
        {
            ctx.DrawImage(card, new Point(cardX, cardY), 0.92f);
            ctx.DrawImage(foto, new Point(cardX + pad, cardY + pad), 1f);
        });

        using var ms = new MemoryStream();
        await img.SaveAsPngAsync(ms, ct);
        return ms.ToArray();
    }
}
