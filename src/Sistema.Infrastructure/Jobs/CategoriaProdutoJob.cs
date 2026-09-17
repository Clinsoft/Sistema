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
        // Vinagre tem prioridade sobre "propolis" (há vinagre de maçã c/ própolis, que deve
        // cair em Ervas/condimentos, não em Suplementos).
        (new[]{"vinagre"}, "Ervas e especiarias"),
        (new[]{"whey","proteina","proteína","colageno","colágeno","creatina","capsula","cápsula","caps ","vitamina","suplement","hydro protein","psyllium","psillium","supercoffee","super coffee","caffeine","cafeina","cafeína","termogenic","bcaa","glutamina","aminoacido","aminoácido","melatonina","triptofano","magnesio","magnésio","zinco","omega","ômega","probiotic","cloreto de magnesio","spirulina","clorella","chlorella","maca peruana","propolis","própolis","verdpropolis"}, "Suplementos"),
        (new[]{"fibra","farelo","goma acacia","goma-acacia"}, "Fibra Alimentar"),
        (new[]{"cha ","chá","infus","erva mate","hibisc","camomila","erva-mate","erva doce","carqueja","boldo","capim cidreira","melissa","kombucha"}, "Chás e infusões"),
        (new[]{"chocolate","cacau","achocolat","brigadeiro","choco "}, "Chocolates"),
        (new[]{"biscoito","cookie","rosquinha","bolacha","wafer","palito de tapioca","dadinho de tapioca","pao de queijo","pão de queijo","pipoquinha de pao de queijo","cracker","torrada"}, "Biscoitos"),
        (new[]{"molho","ketchup","maionese","mostarda","shoyu","pate","patê","calda","barbecue","buffalo","ranch"}, "Molhos"),
        (new[]{"oleo","óleo","azeite","ghee","gordura","manteiga"}, "Gorduras e óleos vegetais"),
        (new[]{"mel ","melado","melaco","melaço","adocante","adoçante","xilitol","eritritol","stevia","agave","acucar","açúcar","doce de leite","goiabada","geleia","geléia","melagriao","melagrião"}, "Mel e adoçantes naturais"),
        (new[]{"tempero","especiaria","pimenta","oregano","orégano","canela","curry","cominho","louro","paprica","páprica","chimichurri","sal marinho","sal rosa","farofa","curcuma","cúrcuma","acafrao","açafrão","gengibre em po","chimichurri","vinagre"}, "Ervas e especiarias"),
        (new[]{"castanha","amendoa","amêndoa","noz","nozes","semente","amendoim","pistache","paçoca","pacoca","pasta de amendoim","chia","linhaca","linhaça","gergelim","girassol","tahine","tahini"}, "Oleaginosas e sementes"),
        (new[]{"uva passa","uvas passas","damasco","cristalizad","crista.","fruta seca","tamara","tâmara","ameixa seca","banana passa","coco ralado","figo seco","gengibre crist","abacaxi crist"}, "Frutas secas e cristalizadas"),
        (new[]{"chips","pipoca","snack","salgadinho"}, "Chips naturais"),
        (new[]{"aveia","quinoa","arroz integral","cuscuz","granola","cereal","grao de bico","grão de bico","tapioca","farinha","flocos","fuba","fubá","fuba de milho","polvilho","amaranto","milho para cuscuz","nhoque","penne","macarrao","macarrão","espaguete","espaguetti","talharim","massa ","parafuso","fusilli"}, "Grãos integrais"),
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

        // Marcas "genéricas" que NÃO servem de sinal de categoria (não têm categoria própria).
        var marcasGenericas = categorias.Count == 0 ? new HashSet<Guid>() :
            (await db.Marcas.Where(m => m.EmpresaId == empresaId
                    && (m.Nome == "Sem marca" || m.Nome == "Geral"))
                .Select(m => m.Id).ToListAsync()).ToHashSet();

        // Categoria DOMINANTE por marca, a partir dos produtos JÁ categorizados (nossa "base").
        // Só considera marca com >=2 produtos categorizados e >=60% numa mesma categoria.
        var porMarca = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.CategoriaId != diversosId)
            .GroupBy(p => new { p.MarcaId, p.CategoriaId })
            .Select(g => new { g.Key.MarcaId, g.Key.CategoriaId, Qtd = g.Count() })
            .ToListAsync();
        var marcaCategoria = porMarca
            .Where(x => !marcasGenericas.Contains(x.MarcaId))
            .GroupBy(x => x.MarcaId)
            .Select(g => { var total = g.Sum(x => x.Qtd); var top = g.OrderByDescending(x => x.Qtd).First();
                           return new { Marca = g.Key, top.CategoriaId, Ok = total >= 2 && top.Qtd * 1.0 / total >= 0.6 }; })
            .Where(x => x.Ok)
            .ToDictionary(x => x.Marca, x => x.CategoriaId);

        Guid? PorRegra(string texto)
        {
            foreach (var (chaves, nomeCat) in Regras)
                if (chaves.Any(k => texto.Contains(Norm(k))) && mapaCat.TryGetValue(Norm(nomeCat), out var cid))
                    return cid;
            return null;
        }

        // ===== PASSO 1: categorização LOCAL (grátis, sem limite, sem Cosmos) =====
        // Regra por palavra-chave na descrição + categoria dominante da marca. Roda sobre TODO
        // o backlog de "Diversos" — economiza consultas ao Cosmos.
        var diversos = await db.Produtos
            .Where(p => p.Ativo && p.CategoriaId == diversosId)
            .ToListAsync();
        int locais = 0;
        foreach (var p in diversos)
        {
            var destino = PorRegra(Norm(p.Descricao));
            if (destino is null && marcaCategoria.TryGetValue(p.MarcaId, out var bcat))
                destino = bcat;
            if (destino is { } d && d != diversosId) { p.DefinirCategoria(d); locais++; }
        }
        if (locais > 0) await db.SaveChangesAsync();

        // ===== PASSO 2: Cosmos (≤20/dia) — só para os que sobraram, com EAN, e p/ CEST =====
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
        {
            logger.LogInformation("[CATEGORIA-PRODUTO] {L} por regra/marca (local). Cota Cosmos do dia já usada.", locais);
            return new ResultadoCategorias(locais, 0, restantes, LimiteDiarioAtingido: true);
        }

        var candidatos = await query.Take(restanteHoje).ToListAsync();
        var agora = DateTime.UtcNow;
        int viaCosmos = 0, tentados = 0;
        foreach (var p in candidatos)
        {
            var cls = await cosmos.ClassificarAsync(p.CodigoBarras!);
            if (!cls.Ok) break; // cota/erro — retoma no próximo dia, sem marcar

            var texto = Norm($"{cls.Categoria} {cls.Gpc} {cls.Descricao} {p.Descricao}");
            var destino = PorRegra(texto);
            p.RegistrarTentativaCategoria(destino, cls.Cest, agora);
            if (destino is not null) viaCosmos++;
            tentados++;
            await Task.Delay(500);
        }
        if (tentados > 0) await db.SaveChangesAsync();

        logger.LogInformation("[CATEGORIA-PRODUTO] {L} por regra/marca (local) + {C} via Cosmos de {T}. Restam {R} em Diversos.",
            locais, viaCosmos, tentados, restantes - tentados);
        return new ResultadoCategorias(locais + viaCosmos, tentados, restantes - tentados, false);
    }
}
