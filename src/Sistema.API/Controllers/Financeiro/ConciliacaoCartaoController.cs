using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>
/// Concilia as vendas de cartão/Pix do sistema (ReceiveisCartao) com o relatório de
/// vendas exportado da operadora (InfinitePay). Casa por DATA + HORA (±min) + VALOR
/// (a hora vem da Venda; o relatório traz a hora da autorização), forma-agnóstico —
/// assim aponta separadamente quando a FORMA foi registrada errada (ex.: Pix lançado
/// como débito/crédito). Sobra: só no sistema / só na operadora.
/// </summary>
[ApiController]
[Route("api/financeiro/conciliacao-cartao")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
public class ConciliacaoCartaoController(SistemaDbContext db) : ControllerBase
{
    private const int ToleranciaMinutos = 5;
    private const decimal ToleranciaValor = 0.02m;

    [HttpPost("importar")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Importar([FromForm] Guid empresaId, IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { mensagem = "Envie o arquivo CSV do relatório de vendas da operadora." });

        var linhas = new List<string>();
        using (var sr = new StreamReader(arquivo.OpenReadStream(), System.Text.Encoding.UTF8, true))
        {
            string? l;
            while ((l = await sr.ReadLineAsync(ct)) != null) linhas.Add(l);
        }
        if (linhas.Count < 2) return BadRequest(new { mensagem = "Arquivo vazio ou sem transações." });

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

        var op = new List<Txn>();
        for (int k = 1; k < linhas.Count; k++)
        {
            if (string.IsNullOrWhiteSpace(linhas[k])) continue;
            var c = ParseLinha(linhas[k]);
            if (c.Count <= iBruto) continue;
            var status = iStatus >= 0 ? c[iStatus].Trim() : "Aprovada";
            if (!status.StartsWith("Aprovad", StringComparison.OrdinalIgnoreCase)
                && !status.Equals("Complete", StringComparison.OrdinalIgnoreCase)) continue;
            if (!TryDataHora(c[iData], out var dh)) continue;
            var forma = NormForma(c[iMeio]); if (forma is null) continue;
            var bruto = ParseValor(c[iBruto]); if (bruto <= 0) continue;
            op.Add(new Txn(dh, forma, bruto,
                iTaxa >= 0 && c.Count > iTaxa ? Math.Abs(ParseValor(c[iTaxa])) : 0,
                iNome >= 0 && c.Count > iNome ? c[iNome].Trim() : null));
        }
        if (op.Count == 0) return BadRequest(new { mensagem = "Nenhuma transação aprovada encontrada no arquivo." });

        var ini = op.Min(t => t.Data).Date;
        var fim = op.Max(t => t.Data).Date;

        // Sistema: recebível + hora da venda (fallback: DataTransacao à meia-noite).
        var sisRaw = await (
            from r in db.ReceiveisCartao.AsNoTracking()
            where r.EmpresaId == empresaId && r.DataTransacao >= ini && r.DataTransacao < fim.AddDays(1)
            join v in db.Vendas.AsNoTracking() on r.VendaId equals v.Id into vj
            from v in vj.DefaultIfEmpty()
            select new { r.DataTransacao, r.FormaPagamento, r.ValorBruto, r.Taxa, Hora = (DateTime?)(v != null ? v.DataHora : r.DataTransacao) }
        ).ToListAsync(ct);
        var sis = sisRaw.Select(r => new SisTxn(
            (r.Hora ?? r.DataTransacao), r.DataTransacao.Date, NormForma(r.FormaPagamento) ?? "?", r.ValorBruto)).ToList();

        // Índice do sistema por data.
        var porDia = sis.GroupBy(s => s.Dia).ToDictionary(g => g.Key, g => g.ToList());

        int casados = 0;
        var formaDivergente = new List<object>();
        var soNaOperadora = new List<Txn>();
        foreach (var t in op.OrderBy(t => t.Data))
        {
            if (!porDia.TryGetValue(t.Data.Date, out var cand)) { soNaOperadora.Add(t); continue; }
            // melhor candidato: mesmo valor (±tol), menor diferença de hora
            SisTxn? melhor = null; double melhorMin = double.MaxValue;
            foreach (var s in cand)
            {
                if (s.Usado || Math.Abs(s.Valor - t.Bruto) > ToleranciaValor) continue;
                var dm = Math.Abs((s.Hora - t.Data).TotalMinutes);
                // sem hora real (venda nula) → dm gigante; ainda casa se forma+valor batem
                var ok = dm <= ToleranciaMinutos || s.Forma == t.Forma;
                if (ok && dm < melhorMin) { melhor = s; melhorMin = dm; }
            }
            if (melhor is null) { soNaOperadora.Add(t); continue; }
            melhor.Usado = true; casados++;
            if (melhor.Forma != t.Forma)
                formaDivergente.Add(new { data = t.Data.Date, valor = t.Bruto, formaSistema = melhor.Forma, formaOperadora = t.Forma, cliente = t.Nome });
        }
        var soNoSistema = sis.Where(s => !s.Usado).ToList();

        string[] formas = { "Débito", "Crédito", "Pix" };
        var resumo = formas.Select(f => new
        {
            forma = f,
            operadoraQtd = op.Count(t => t.Forma == f),
            operadoraBruto = Math.Round(op.Where(t => t.Forma == f).Sum(t => t.Bruto), 2),
            operadoraTaxa = Math.Round(op.Where(t => t.Forma == f).Sum(t => t.Taxa), 2),
            sistemaQtd = sis.Count(t => t.Forma == f),
            sistemaBruto = Math.Round(sis.Where(t => t.Forma == f).Sum(t => t.Valor), 2),
        }).ToList();

        return Ok(new
        {
            periodo = new { inicio = ini, fim },
            totalOperadora = op.Count,
            totalSistema = sis.Count,
            casados,
            resumo,
            taxaTotalOperadora = Math.Round(op.Sum(t => t.Taxa), 2),
            formaDivergente,
            soNoSistema = soNoSistema.OrderBy(s => s.Dia).ThenBy(s => s.Forma)
                .Select(s => new { data = s.Dia, s.Forma, valor = s.Valor }).ToList(),
            soNaOperadora = soNaOperadora.OrderBy(t => t.Data).ThenBy(t => t.Forma)
                .Select(t => new { data = t.Data.Date, t.Forma, valor = t.Bruto, t.Nome }).ToList(),
        });
    }

    // ─── modelos e helpers ───────────────────────────────────────────────────
    private record Txn(DateTime Data, string Forma, decimal Bruto, decimal Taxa, string? Nome);
    private sealed class SisTxn(DateTime hora, DateTime dia, string forma, decimal valor)
    {
        public DateTime Hora { get; } = hora; public DateTime Dia { get; } = dia;
        public string Forma { get; } = forma; public decimal Valor { get; } = valor;
        public bool Usado { get; set; }
    }

    private static string? NormForma(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        if (s.Contains("créd") || s.Contains("cred")) return "Crédito";
        if (s.Contains("déb") || s.Contains("deb")) return "Débito";
        if (s.Contains("pix")) return "Pix";
        return null;
    }

    private static bool TryDataHora(string s, out DateTime d)
    {
        s = (s ?? "").Trim();
        foreach (var fmt in new[] { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                                    "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" })
            if (DateTime.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return true;
        return DateTime.TryParse(s, new CultureInfo("pt-BR"), DateTimeStyles.None, out d);
    }

    private static decimal ParseValor(string s)
    {
        s = (s ?? "").Replace("R$", "").Replace("'", "").Replace("+", "").Replace(" ", "").Trim()
                     .Replace(".", "").Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private static List<string> ParseLinha(string linha)
    {
        var res = new List<string>(); var sb = new System.Text.StringBuilder(); bool asp = false;
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
