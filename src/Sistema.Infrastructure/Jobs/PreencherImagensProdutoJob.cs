using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;

namespace Sistema.Infrastructure.Jobs;

/// <summary>Resultado de uma rodada de preenchimento de imagens.</summary>
public record ResultadoImagens(int Preenchidos, int Tentados, int RestantesCatalogo, bool LimiteDiarioAtingido);

/// <summary>
/// Preenche fotos de produto pelo código de barras (via <see cref="ImagemProdutoService"/>:
/// Cosmos + fallback Open Food Facts) e salva cópia local. Respeita o limite GRÁTIS de
/// 20 consultas/dia do Cosmos: processa no máximo 20 produtos por dia, marcando cada um
/// como "tentado" (ImagemBuscadaEm) para não repetir nem desperdiçar cota. Roda todo dia
/// até acabar os produtos com EAN e sem foto. O limite é compartilhado entre o job diário
/// e o botão manual — ambos consomem do mesmo teto de 20/dia.
/// </summary>
public class PreencherImagensProdutoJob(
    SistemaDbContext db, ImagemProdutoService imagem, ILogger<PreencherImagensProdutoJob> logger)
{
    public const int MaxPorDia = 20;

    [AutomaticRetry(Attempts = 0)] // não reprocessa em falha — gastaria cota
    public async Task<ResultadoImagens> ExecutarAsync()
    {
        var candidatosQuery = db.Produtos
            .Where(p => p.Ativo
                     && p.CodigoBarras != null && p.CodigoBarras != "" && p.CodigoBarras.Length >= 8
                     && (p.ImagemUrl == null || p.ImagemUrl == "")
                     && p.ImagemBuscadaEm == null)
            .OrderBy(p => p.Codigo);

        var restantesCatalogo = await candidatosQuery.CountAsync();

        // Quanto da cota de hoje ainda sobra (conta produtos tentados hoje, em UTC).
        var hojeUtc = DateTime.UtcNow.Date;
        var usadosHoje = await db.Produtos.CountAsync(p => p.ImagemBuscadaEm != null && p.ImagemBuscadaEm >= hojeUtc);
        var restanteHoje = MaxPorDia - usadosHoje;

        if (restanteHoje <= 0)
        {
            logger.LogInformation("[IMAGEM-PRODUTO] Cota diária de {Max} já usada; nada a fazer.", MaxPorDia);
            return new ResultadoImagens(0, 0, restantesCatalogo, LimiteDiarioAtingido: true);
        }

        var candidatos = await candidatosQuery.Take(restanteHoje).ToListAsync();
        if (candidatos.Count == 0)
            return new ResultadoImagens(0, 0, 0, false);

        var dir = Path.Combine("wwwroot", "uploads", "produtos");
        Directory.CreateDirectory(dir);
        var agora = DateTime.UtcNow;
        int preenchidos = 0;

        foreach (var p in candidatos)
        {
            (byte[] Bytes, string Mime, string? Nome)? achado;
            try { achado = await imagem.BuscarAsync(p.CodigoBarras!); }
            catch { achado = null; }

            if (achado is not null)
            {
                var (bytes, mime, _) = achado.Value;
                var ext = mime switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
                var nomeArq = $"{p.Id}{ext}";
                await File.WriteAllBytesAsync(Path.Combine(dir, nomeArq), bytes);
                p.RegistrarTentativaImagem($"/uploads/produtos/{nomeArq}", agora);
                preenchidos++;
            }
            else
            {
                p.RegistrarTentativaImagem(null, agora); // marca tentado (não achou) p/ não repetir
            }

            await Task.Delay(500); // ritmo gentil com a API
        }

        await db.SaveChangesAsync();
        logger.LogInformation("[IMAGEM-PRODUTO] {P} foto(s) de {T} tentativa(s). Restam {R} no catálogo.",
            preenchidos, candidatos.Count, restantesCatalogo - candidatos.Count);

        return new ResultadoImagens(preenchidos, candidatos.Count, restantesCatalogo - candidatos.Count, false);
    }
}
