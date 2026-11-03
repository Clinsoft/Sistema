using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Desempenho.Entities;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Penaliza o desempenho quando uma conversa do WhatsApp fica SEM resposta por mais de 6 horas
/// de HORÁRIO DE FUNCIONAMENTO (Seg–Sáb 8–21h, Dom 8–14h). Desconta pontos de TODOS os atendentes
/// da loja dona do número. Idempotente: cada mensagem sem resposta gera no máximo uma penalidade
/// por atendente. Roda de hora em hora.
///
/// Só penaliza quando há de fato uma PERGUNTA/PEDIDO do cliente sem resposta. NÃO conta como turno
/// pendente: saudação/cortesia pura (oi, bom dia, obrigada, blz, emoji), auto-resposta do próprio
/// contato ("agradeço seu contato, retorno em breve"), link compartilhado sem texto, figurinha,
/// reação, e mídia (foto/vídeo/documento) sem legenda. Áudio (voz) conta como pergunta real.
/// Quando o cliente abre com uma saudação e só depois manda a pergunta, é a PERGUNTA que vale.
/// </summary>
public class WhatsAppSlaJob(SistemaDbContext db, ILogger<WhatsAppSlaJob> logger)
{
    private const decimal PontosPorOcorrencia = 10m;
    private const int PrazoMinutosUteis = 360;   // 6 horas de funcionamento

    // Só penaliza conversas iniciadas a partir da ativação da regra (UTC). Ocorrências
    // anteriores a esta data são ignoradas.
    private static readonly DateTime AtivadoEmUtc = new(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc);

    private static readonly TimeZoneInfo Tz = ResolverTz();
    private static TimeZoneInfo ResolverTz()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
    }
    private static DateTime ParaLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tz);

    // Janela de funcionamento do dia (local). Domingo 8–14; Seg–Sáb 8–21.
    private static (DateTime abre, DateTime fecha) Janela(DateTime dia)
    {
        var abre = dia.Date.AddHours(8);
        var fecha = dia.DayOfWeek == DayOfWeek.Sunday ? dia.Date.AddHours(14) : dia.Date.AddHours(21);
        return (abre, fecha);
    }

    /// <summary>Minutos de HORÁRIO DE FUNCIONAMENTO entre dois instantes locais.</summary>
    private static double MinutosUteis(DateTime aLocal, DateTime bLocal)
    {
        if (bLocal <= aLocal) return 0;
        double total = 0;
        for (var dia = aLocal.Date; dia <= bLocal.Date; dia = dia.AddDays(1))
        {
            var (abre, fecha) = Janela(dia);
            var ini = aLocal > abre ? aLocal : abre;
            var fim = bLocal < fecha ? bLocal : fecha;
            if (fim > ini) total += (fim - ini).TotalMinutes;
        }
        return total;
    }

    // Palavras de saudação/cortesia/fechamento (normalizadas: minúsculas, sem acento). Se a mensagem
    // do cliente tiver só essas palavras — ou só emoji —, é saudação/fechamento, não pergunta.
    private static readonly HashSet<string> CortesiaPalavras = new(StringComparer.Ordinal)
    {
        // agradecimento / fechamento
        "obrigada","obrigado","obrigadao","obrigadinha","obrigadinho","obg","obga","obgda","obgado",
        "obrgd","obrgda","obrgado","obgd","brigada","brigado","brigadao","grata","grato","agradecida",
        "agradecido","agradeco","vlw","valeu","valew","flw","falou","ok","oka","okk","okay","okey","okj",
        "blz","beleza","certo","certinho","perfeito","perfeita","otimo","otima","show","showw","joia",
        "massa","top","maravilha","maravilhosa","entendi","entendido","entendida","muito","mto","mt",
        "tudo","bem","ta","tah","de","nada","isso","mesmo","demais","entao","amem","amen",
        "abencoe","deus","por","graca","gracas","uhum","aham","combinado","fechado","fechou","beijos",
        "bjs","bjos","bjus","abraco","abracos","otimaa","legal","maravilhoso","sucesso","vlww",
        // saudação / abertura (sem pergunta)
        "oi","ola","oie","oin","opa","eai","eae","alo","bom","boa","dia","tarde","noite",
        "moca","mocinha","moca","gente","pessoal","prezado","prezada","prezados","prezadas",
        "senhor","senhora","sr","sra","bemvindo","bemvinda"
    };

    // Trechos típicos de AUTO-RESPOSTA do próprio contato (empresa com responder automático).
    // Não há o que responder — não penaliza. Comparado sobre o texto normalizado (sem acento).
    private static readonly string[] PadroesAutoResposta =
    {
        "agradeco seu contato","obrigado pelo seu contato","obrigada pelo seu contato",
        "agradecemos seu contato","agradeco o seu contato","obrigado pelo contato",
        "no momento nao posso","nao posso responder","nao posso atender","retornarei",
        "breve eu retorno","breve retorno","retorno o mais breve","assim que possivel",
        "mensagem automatica","resposta automatica","horario de atendimento","retornaremos"
    };

    private static string Normalizar(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue; // remove acento
            if (char.IsLetter(ch)) sb.Append(ch);
            else if (char.IsWhiteSpace(ch)) sb.Append(' ');
            // dígitos, pontuação e emoji são ignorados (viram separador implícito)
        }
        return Regex.Replace(sb.ToString(), " +", " ").Trim();
    }

    /// <summary>Mensagem do cliente que é só saudação/cortesia (oi/bom dia/obrigada/ok/emoji), não pergunta.</summary>
    private static bool EhCortesiaOuSaudacao(string? texto)
    {
        if (texto == null) return false;
        if (texto.Contains('?') || texto.Any(char.IsDigit)) return false;  // pergunta/número → conteúdo real
        var norm = Normalizar(texto);
        var palavras = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palavras.Length == 0) return true;             // só emoji/pontuação → fechamento
        if (palavras.Length > 8) return false;             // frase longa → provável pergunta/pedido
        return palavras.All(CortesiaPalavras.Contains);
    }

    /// <summary>Texto que é só um link compartilhado (sem pergunta nem texto real).</summary>
    private static bool EhSomenteLink(string texto)
    {
        var semUrl = Regex.Replace(texto, @"https?://\S+|www\.\S+", " ").Trim();
        if (semUrl.Length == 0) return true;
        return !semUrl.Any(char.IsLetter);   // sobrou só pontuação/número → era só link
    }

    // Tipos que nunca são um turno do cliente aguardando resposta.
    private static readonly HashSet<string> TiposSemConteudo = new(StringComparer.Ordinal)
    { "reaction", "unsupported", "sticker" };

    /// <summary>A mensagem NÃO é uma pergunta/pedido do cliente que exija resposta (não penaliza).</summary>
    public static bool EhIgnoravel(string? tipo, string? texto)
    {
        tipo = string.IsNullOrEmpty(tipo) ? "text" : tipo;
        if (TiposSemConteudo.Contains(tipo)) return true;
        var t = (texto ?? "").Trim();
        if (t.Length == 0)
            return tipo != "audio";   // áudio (voz) sem texto = pergunta real; outra mídia sem legenda = sem contexto
        if (t.Contains('?')) return false;                       // pergunta explícita → conteúdo real
        var norm = Normalizar(t);
        if (norm.Length == 0) return true;                       // só emoji/pontuação
        if (PadroesAutoResposta.Any(p => norm.Contains(p))) return true;  // auto-resposta do contato
        if (EhSomenteLink(t)) return true;                       // link compartilhado sem pergunta
        return EhCortesiaOuSaudacao(t);                          // saudação/cortesia pura
    }

    private sealed record Msg(Guid Id, Guid EmpresaId, Guid Loja, string Telefone,
        DirecaoMensagemWhatsApp Direcao, DateTime DataHora, string? Texto, string Tipo);

    /// <summary>Execução horária: avalia conversas dos últimos 10 dias.</summary>
    public async Task ExecutarAsync()
    {
        var agoraLocal = ParaLocal(DateTime.UtcNow);
        var (_, criadas) = await ProcessarAsync(DateTime.UtcNow.AddDays(-10), null, agoraLocal,
            empresaFiltro: null, limparMes: null);
        if (criadas > 0)
            logger.LogInformation("[WhatsAppSLA] {N} conversas sem resposta em 6h úteis penalizadas.", criadas);
    }

    /// <summary>Reprocessa um mês fechado: apaga as penalidades do mês e reavalia com as regras atuais.
    /// (O job horário só olha 10 dias; isto permite corrigir meses passados.)</summary>
    public async Task<(int removidas, int criadas)> ReprocessarMesAsync(Guid empresaId, int ano, int mes)
    {
        var iniLocal = new DateTime(ano, mes, 1);
        // Janela em UTC com folga de 1 dia para cada lado (fuso BRT = −3) — cobre o mês local inteiro.
        var desdeUtc = DateTime.SpecifyKind(iniLocal.AddDays(-1), DateTimeKind.Utc);
        var ateUtc = DateTime.SpecifyKind(iniLocal.AddMonths(1).AddDays(1), DateTimeKind.Utc);
        return await ProcessarAsync(desdeUtc, ateUtc, ParaLocal(DateTime.UtcNow),
            empresaFiltro: empresaId, limparMes: (ano, mes));
    }

    private async Task<(int removidas, int criadas)> ProcessarAsync(
        DateTime desdeUtc, DateTime? ateUtc, DateTime agoraLocal,
        Guid? empresaFiltro, (int ano, int mes)? limparMes)
    {
        var atendentesPorLoja = (await db.Usuarios.AsNoTracking()
            .Where(u => u.Ativo && u.Perfil == "Atendente" && u.LocalEstoqueId != null
                     && (empresaFiltro == null || u.EmpresaId == empresaFiltro))
            .Select(u => new { u.Id, u.EmpresaId, Loja = u.LocalEstoqueId!.Value })
            .ToListAsync())
            .GroupBy(u => u.Loja)
            .ToDictionary(g => g.Key, g => g.ToList());
        if (atendentesPorLoja.Count == 0) return (0, 0);
        var lojas = atendentesPorLoja.Keys.ToList();

        int removidas = 0;
        if (limparMes is { } lm && empresaFiltro is { } emp)
        {
            var velhas = await db.PenalidadesAtendimentoWhatsApp
                .Where(p => p.EmpresaId == emp && p.Ano == lm.ano && p.Mes == lm.mes)
                .ToListAsync();
            if (velhas.Count > 0)
            {
                db.PenalidadesAtendimentoWhatsApp.RemoveRange(velhas);
                await db.SaveChangesAsync();   // some do banco antes de reavaliar/dedup
                removidas = velhas.Count;
            }
        }

        var q = db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.LocalEstoqueId != null && lojas.Contains(m.LocalEstoqueId.Value)
                     && m.DataHora >= desdeUtc
                     // já descarta tipos sem conteúdo na origem (reação/figurinha/não suportado)
                     && m.Tipo != "reaction" && m.Tipo != "unsupported" && m.Tipo != "sticker"
                     && (m.MidiaUrl != null || (m.Texto != null && m.Texto.Trim() != "")));
        if (ateUtc != null) q = q.Where(m => m.DataHora < ateUtc);
        var msgs = (await q
            .Select(m => new { m.Id, m.EmpresaId, Loja = m.LocalEstoqueId!.Value, m.Telefone, m.Direcao, m.DataHora, m.Texto, m.Tipo })
            .OrderBy(m => m.DataHora).ToListAsync())
            .Select(m => new Msg(m.Id, m.EmpresaId, m.Loja, m.Telefone, m.Direcao, m.DataHora, m.Texto, m.Tipo))
            .ToList();

        // Números bloqueados (spam) da empresa — não penaliza conversas com eles.
        var bloqueados = (await db.NumerosBloqueadosWhatsApp.AsNoTracking()
            .Where(b => empresaFiltro == null || b.EmpresaId == empresaFiltro)
            .Select(b => b.Telefone).ToListAsync()).ToHashSet();

        var jaPenalizadas = (await db.PenalidadesAtendimentoWhatsApp.AsNoTracking()
            .Where(p => p.DataOcorrencia >= agoraLocal.AddDays(-40))
            .Select(p => p.MensagemId).Distinct().ToListAsync())
            .ToHashSet();

        int novas = 0;
        foreach (var conv in msgs.GroupBy(m => (m.Loja, m.Telefone)))
        {
            if (bloqueados.Contains(SoDigitos(conv.Key.Telefone))) continue;
            var ord = conv.OrderBy(m => m.DataHora).ToList();
            for (int i = 0; i < ord.Count; i++)
            {
                if (ord[i].Direcao != DirecaoMensagemWhatsApp.Recebida) continue;
                // Só o INÍCIO de um turno do cliente (a anterior foi enviada por nós, ou é a 1ª).
                if (i > 0 && ord[i - 1].Direcao == DirecaoMensagemWhatsApp.Recebida) continue;

                // Dentro do turno (mensagens recebidas consecutivas), a 1ª que é PERGUNTA de verdade.
                Msg? pergunta = null;
                for (int j = i; j < ord.Count && ord[j].Direcao == DirecaoMensagemWhatsApp.Recebida; j++)
                {
                    var c = ord[j];
                    if (c.DataHora < AtivadoEmUtc) continue;
                    if (EhIgnoravel(c.Tipo, c.Texto)) continue;
                    pergunta = c;
                    break;
                }
                if (pergunta == null) continue;                 // turno só de saudação/cortesia/link/mídia
                if (jaPenalizadas.Contains(pergunta.Id)) continue;

                var recebidaLocal = ParaLocal(pergunta.DataHora);
                var resposta = ord.SkipWhile(x => x.DataHora <= pergunta.DataHora)
                    .FirstOrDefault(x => x.Direcao == DirecaoMensagemWhatsApp.Enviada);
                var fimLocal = resposta != null ? ParaLocal(resposta.DataHora) : agoraLocal;

                var minutos = MinutosUteis(recebidaLocal, fimLocal);
                var respondidaTarde = resposta != null && minutos > PrazoMinutosUteis;
                var vencidaSemResposta = resposta == null && minutos > PrazoMinutosUteis;
                if (!respondidaTarde && !vencidaSemResposta) continue;

                foreach (var at in atendentesPorLoja[pergunta.Loja])
                    db.PenalidadesAtendimentoWhatsApp.Add(PenalidadeAtendimentoWhatsApp.Criar(
                        pergunta.EmpresaId, pergunta.Loja, at.Id, recebidaLocal, pergunta.Telefone, pergunta.Id, PontosPorOcorrencia));
                jaPenalizadas.Add(pergunta.Id);
                novas++;
            }
        }

        if (novas > 0) await db.SaveChangesAsync();
        return (removidas, novas);
    }

    private static string SoDigitos(string s) => new(s.Where(char.IsDigit).ToArray());
}
