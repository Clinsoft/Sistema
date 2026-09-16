using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Services;

namespace Sistema.Infrastructure.Jobs;

public record ResultadoCategorias(int Categorizados, int Tentados, int RestantesCatalogo, bool LimiteDiarioAtingido);

/// <summary>
/// Categoriza automaticamente os produtos que estão em "Diversos" a partir do EAN (Cosmos:
/// NCM/GPC/categoria + a própria descrição do produto), mapeando para as categorias JÁ
/// existentes da loja. Respeita a cota grátis do Cosmos (20/dia, COMPARTILHADA com o job de
/// fotos): processa até 20/dia, marcando cada um como tentado (CategoriaBuscadaEm) para não
/// repetir. Só altera produtos em "Diversos" (não sobrescreve categorização manual). Se a
/// cota estourar (429), para e retoma no dia seguinte.
/// </summary>
public class CategoriaProdutoJob(
    SistemaDbContext db, ImagemProdutoService cosmos, ILogger<CategoriaProdutoJob> logger)
{
    public const int MaxPorDia = 20;

    // Regras de palavra-chave → nome da categoria da loja (ordem: mais específica primeiro).
    // Só aplica se a categoria existir no cadastro da empresa.
    private static readonly (string[] Chaves, string Categoria)[] Regras =
    {
        (new[]{"whey","proteina","colageno","creatina","capsula","caps ","vitamina","suplement","hydro protein","psyllium","psillium"}, "Suplementos"),
        (new[]{"fibra","farelo"}, "Fibra Alimentar"),
        (new[]{"cha ","chá","infus","erva mate","hibisc","camomila","erva-mate"}, "Chás e infusões"),
        (new[]{"chocolate","cacau","achocolat","brigadeiro","choco "}, "Chocolates"),
        (new[]{"biscoito","cookie","rosquinha","bolacha","wafer","palito de tapioca","dadinho de tapioca","pao de queijo","pão de queijo"}, "Biscoitos"),
        (new[]{"molho","ketchup","maionese","mostarda","shoyu","pate","patê","calda"}, "Molhos"),
        (new[]{"oleo","óleo","azeite","ghee","gordura","manteiga"}, "Gorduras e óleos vegetais"),
        (new[]{"mel ","melado","melaco","adocante","adoçante","xilitol","eritritol","stevia","agave","acucar","açúcar","doce de leite","goiabada"}, "Mel e adoçantes naturais"),
        (new[]{"tempero","especiaria","pimenta","oregano","orégano","canela","curry","cominho","louro","paprica","páprica","chimichurri","sal marinho","farofa"}, "Ervas e especiarias"),
        (new[]{"castanha","amendoa","amêndoa","noz","nozes","semente","amendoim","pistache","paçoca","pacoca","pasta de amendoim"}, "Oleaginosas e sementes"),
        (new[]{"uva passa","uvas passas","damasco","cristalizad","fruta seca","tamara","tâmara","ameixa seca","banana passa"}, "Frutas secas e cristalizadas"),
        (new[]{"chips","pipoca","snack","salgadinho"}, "Chips naturais"),
        (new[]{"aveia","quinoa","arroz integral","cuscuz","granola","cereal","grao de bico","grão de bico","tapioca","farinha","flocos"}, "Grãos integrais"),
    };

    private static string Norm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task<ResultadoCategorias> ExecutarAsync()
    {
        var empresaId = await db.Produtos.Select(p => p.EmpresaId).FirstOrDefaultAsync();
        // Mapa nome-normalizado -> Id das categorias da loja.
        var categorias = await db.Categorias.Where(c => c.EmpresaId == empresaId && c.Ativo)
            .Select(c => new { c.Id, c.Nome }).ToListAsync();
        var mapaCat = categorias.ToDictionary(c => Norm(c.Nome), c => c.Id);
        var diversosId = categorias.FirstOrDefault(c => Norm(c.Nome) == "diversos")?.Id;
        if (diversosId is null) return new ResultadoCategorias(0, 0, 0, false);

        // Só produtos em "Diversos", com EAN válido e ainda não tentados.
        var query = db.Produtos
            .Where(p => p.Ativo && p.CategoriaId == diversosId && p.CategoriaBuscadaEm == null
                     && p.CodigoBarras != null && p.CodigoBarras != ""
                     && (p.CodigoBarras.Length == 8 || p.CodigoBarras.Length == 12
                         || p.CodigoBarras.Length == 13 || p.CodigoBarras.Length == 14)
                     && !EF.Functions.Like(p.CodigoBarras, "%[^0-9]%"))
            .OrderBy(p => p.Codigo);

        var restantes = await query.CountAsync();

        var hojeUtc = DateTime.UtcNow.Date;
        var usadosHoje = await db.Produtos.CountAsync(p => p.CategoriaBuscadaEm != null && p.CategoriaBuscadaEm >= hojeUtc);
        var restanteHoje = MaxPorDia - usadosHoje;
        if (restanteHoje <= 0)
            return new ResultadoCategorias(0, 0, restantes, LimiteDiarioAtingido: true);

        var candidatos = await query.Take(restanteHoje).ToListAsync();
        if (candidatos.Count == 0) return new ResultadoCategorias(0, 0, 0, false);

        var agora = DateTime.UtcNow;
        int categorizados = 0, tentados = 0;
        foreach (var p in candidatos)
        {
            var cls = await cosmos.ClassificarAsync(p.CodigoBarras!);
            if (!cls.Ok) break; // cota/erro de comunicação — retoma amanhã, sem marcar

            // Texto combinado: categoria/GPC do Cosmos + descrição do Cosmos + a própria descrição.
            var texto = Norm($"{cls.Categoria} {cls.Gpc} {cls.Descricao} {p.Descricao}");
            Guid? destino = null;
            foreach (var (chaves, nomeCat) in Regras)
            {
                if (chaves.Any(k => texto.Contains(Norm(k))) && mapaCat.TryGetValue(Norm(nomeCat), out var cid))
                { destino = cid; break; }
            }

            p.RegistrarTentativaCategoria(destino, cls.Cest, agora);
            if (destino is not null) categorizados++;
            tentados++;
            await Task.Delay(500); // ritmo gentil com a API
        }

        await db.SaveChangesAsync();
        logger.LogInformation("[CATEGORIA-PRODUTO] {C} categorizado(s) de {T} tentativa(s). Restam {R} em Diversos.",
            categorizados, tentados, restantes - tentados);
        return new ResultadoCategorias(categorizados, tentados, restantes - tentados, false);
    }
}
