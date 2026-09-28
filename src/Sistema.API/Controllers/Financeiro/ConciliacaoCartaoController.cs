using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>
/// Concilia as vendas de cartão/Pix do sistema (ReceiveisCartao) com o relatório de
/// vendas exportado da operadora (InfinitePay). Casa por DATA + FORMA + VALOR BRUTO
/// (o sistema não guarda NSU), e aponta o que está só de um lado + a taxa retida.
/// </summary>
[ApiController]
[Route("api/financeiro/conciliacao-cartao")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
public class ConciliacaoCartaoController(SistemaDbContext db) : ControllerBase
{
    [HttpPost("importar")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Importar([FromForm] Guid empresaId, IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { mensagem = "Envie o arquivo CSV do relatório de vendas da operadora." });

        // ── 1) Lê e parseia o CSV da operadora ──────────────────────────────
        var linhas = new List<string>();
        using (var sr = new StreamReader(arquivo.OpenReadStream(), System.Text.Encoding.UTF8, true))
        {
            string? l;
            while ((l = await sr.ReadLineAsync(ct)) != null) linhas.Add(l);
        }
        if (linhas.Count < 2)
            return BadRequest(new { mensagem = "Arquivo vazio ou sem transações." });

        var header = ParseLinha(linhas[0]);
        int Col(params string[] nomes)
        {
            for (int i = 0; i < header.Count; i++)
                foreach (var n in nomes)
                    if (header[i].Trim().Equals(n, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
        int iData = Col("Data e hora", "Data"), iMeio = Col("Meio - Meio", "Método", "Metodo"),
            iStatus = Col("Status"), iBruto = Col("Valor (R$)", "Valor Bruto (R$)", "Valor"),
            iTaxa = Col("Taxa Aplicada - Valor(R$)", "Taxa"), iNsu = Col("NSU"),
            iNome = Col("Origem - Nome", "Comprador", "Nome");
        if (iData < 0 || iMeio < 0 || iBruto < 0)
            return BadRequest(new { mensagem = "Cabeçalho não reconhecido. Use o CSV de 'Relatório de Vendas' da InfinitePay." });

        var opTxns = new List<Txn>();
        for (int k = 1; k < linhas.Count; k++)
        {
            if (string.IsNullOrWhiteSpace(linhas[k])) continue;
            var c = ParseLinha(linhas[k]);
            if (c.Count <= iBruto) continue;
            var status = iStatus >= 0 ? c[iStatus].Trim() : "Aprovada";
            if (!status.StartsWith("Aprovad", StringComparison.OrdinalIgnoreCase)
                && !status.Equals("Complete", StringComparison.OrdinalIgnoreCase)) continue;
            if (!TryData(c[iData], out var data)) continue;
            var forma = NormForma(c[iMeio]);
            if (forma is null) continue;
            var bruto = ParseValor(c[iBruto]);
            if (bruto <= 0) continue;
            opTxns.Add(new Txn(data, forma, bruto,
                iTaxa >= 0 && c.Count > iTaxa ? Math.Abs(ParseValor(c[iTaxa])) : 0,
                iNsu >= 0 && c.Count > iNsu ? c[iNsu].Trim() : null,
                iNome >= 0 && c.Count > iNome ? c[iNome].Trim() : null));
        }
        if (opTxns.Count == 0)
            return BadRequest(new { mensagem = "Nenhuma transação aprovada encontrada no arquivo." });

        var ini = opTxns.Min(t => t.Data);
        var fim = opTxns.Max(t => t.Data);

        // ── 2) Vendas do sistema no mesmo período ───────────────────────────
        var sis = await db.ReceiveisCartao.AsNoTracking()
            .Where(r => r.EmpresaId == empresaId
                     && r.DataTransacao >= ini && r.DataTransacao < fim.AddDays(1))
            .Select(r => new { r.DataTransacao, r.FormaPagamento, r.ValorBruto, r.Taxa })
            .ToListAsync(ct);
        var sisTxns = sis.Select(r => new Txn(r.DataTransacao.Date, NormForma(r.FormaPagamento) ?? "?",
                                              r.ValorBruto, r.Taxa, null, null)).ToList();

        // ── 3) Casamento por (data, forma, valor) — greedy multiset ─────────
        var mapaSis = new Dictionary<string, List<Txn>>();
        foreach (var t in sisTxns)
            (mapaSis.TryGetValue(Chave(t), out var lst) ? lst : mapaSis[Chave(t)] = new()).Add(t);

        var soNaOperadora = new List<Txn>();
        int casados = 0;
        foreach (var t in opTxns)
        {
            if (mapaSis.TryGetValue(Chave(t), out var lst) && lst.Count > 0)
            { lst.RemoveAt(lst.Count - 1); casados++; }
            else soNaOperadora.Add(t);
        }
        var soNoSistema = mapaSis.Values.SelectMany(v => v).ToList();

        // ── 4) Resumo por forma ─────────────────────────────────────────────
        string[] formas = { "Débito", "Crédito", "Pix" };
        var resumo = formas.Select(f => new
        {
            forma = f,
            operadoraQtd = opTxns.Count(t => t.Forma == f),
            operadoraBruto = Math.Round(opTxns.Where(t => t.Forma == f).Sum(t => t.Bruto), 2),
            operadoraTaxa = Math.Round(opTxns.Where(t => t.Forma == f).Sum(t => t.Taxa), 2),
            sistemaQtd = sisTxns.Count(t => t.Forma == f),
            sistemaBruto = Math.Round(sisTxns.Where(t => t.Forma == f).Sum(t => t.Bruto), 2),
        }).ToList();

        return Ok(new
        {
            periodo = new { inicio = ini, fim },
            totalOperadora = opTxns.Count,
            totalSistema = sisTxns.Count,
            casados,
            resumo,
            taxaTotalOperadora = Math.Round(opTxns.Sum(t => t.Taxa), 2),
            soNoSistema = soNoSistema.OrderBy(t => t.Data).ThenBy(t => t.Forma)
                .Select(t => new { data = t.Data, t.Forma, valor = t.Bruto }).ToList(),
            soNaOperadora = soNaOperadora.OrderBy(t => t.Data).ThenBy(t => t.Forma)
                .Select(t => new { data = t.Data, t.Forma, valor = t.Bruto, t.Nsu, t.Nome }).ToList(),
        });
    }

    // ─── helpers ───────────────────────────────────────────────────────────
    private record Txn(DateTime Data, string Forma, decimal Bruto, decimal Taxa, string? Nsu, string? Nome);
    private static string Chave(Txn t) => $"{t.Data:yyyyMMdd}|{t.Forma}|{t.Bruto:0.00}";

    private static string? NormForma(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        if (s.Contains("créd") || s.Contains("cred")) return "Crédito";
        if (s.Contains("déb") || s.Contains("deb")) return "Débito";
        if (s.Contains("pix")) return "Pix";
        return null;
    }

    private static bool TryData(string s, out DateTime d)
    {
        s = (s ?? "").Trim();
        // aceita "dd/MM/yyyy HH:mm" ou "dd/MM/yyyy" ou "yyyy-MM-dd..."
        var parte = s.Length >= 10 ? s[..10] : s;
        foreach (var fmt in new[] { "dd/MM/yyyy", "yyyy-MM-dd" })
            if (DateTime.TryParseExact(parte, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return true;
        return DateTime.TryParse(s, new CultureInfo("pt-BR"), DateTimeStyles.None, out d);
    }

    private static decimal ParseValor(string s)
    {
        s = (s ?? "").Replace("R$", "").Replace("'", "").Replace("+", "").Replace(" ", "").Trim();
        // pt-BR: milhar '.', decimal ',' → tira '.', troca ',' por '.'
        s = s.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    /// <summary>Parser de uma linha CSV respeitando aspas (valores "5,71" com vírgula dentro).</summary>
    private static List<string> ParseLinha(string linha)
    {
        var res = new List<string>(); var sb = new System.Text.StringBuilder(); bool asp = false;
        // detecta separador: se tem ';' fora de aspas usa ';', senão ','
        char sep = linha.Contains(';') && !linha.Contains("\",\"") ? ';' : ',';
        for (int i = 0; i < linha.Length; i++)
        {
            char ch = linha[i];
            if (ch == '"') { if (asp && i + 1 < linha.Length && linha[i + 1] == '"') { sb.Append('"'); i++; } else asp = !asp; }
            else if (ch == sep && !asp) { res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        res.Add(sb.ToString());
        return res;
    }
}
