using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;
using UglyToad.PdfPig;

namespace Sistema.API.Controllers.Financeiro;

/// <summary>Financiamentos: cadastro, análise de PDF do contrato e comprometimento mensal.</summary>
[ApiController]
[Route("api/financeiro/financiamentos")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
public class FinanciamentosController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    // ── Comprometimento mensal ───────────────────────────────────────────────
    [HttpGet("comprometimento")]
    public async Task<IActionResult> Comprometimento([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var parcelas = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == empresaId
                && l.Tipo == TipoLancamento.ContaPagar
                && l.Categoria == "Financiamentos"
                && (l.Status == StatusLancamento.EmAberto || l.Status == StatusLancamento.PagoParcialmente))
            .Select(l => new { l.Descricao, l.ValorOriginal, l.ValorJuros, l.DataVencimento })
            .ToListAsync(ct);

        static string Contrato(string desc)
        {
            var i = desc.LastIndexOf(" - ", StringComparison.Ordinal);
            return i > 0 ? desc[..i].Trim() : desc.Trim();
        }

        var itens = parcelas.Select(p => new
        {
            contrato = Contrato(p.Descricao),
            p.DataVencimento,
            parcela = p.ValorOriginal,
            juros = p.ValorJuros ?? 0m,
            amortizacao = p.ValorOriginal - (p.ValorJuros ?? 0m)
        }).OrderBy(x => x.DataVencimento).ToList();

        var meses = new[] { "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez" };

        var totalPrincipal = itens.Sum(x => x.amortizacao);
        decimal saldo = totalPrincipal;
        var timeline = itens
            .GroupBy(x => new { x.DataVencimento.Year, x.DataVencimento.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var amort = g.Sum(x => x.amortizacao);
                var linha = new
                {
                    mes = $"{g.Key.Year:0000}-{g.Key.Month:00}",
                    label = $"{meses[g.Key.Month - 1]}/{g.Key.Year % 100:00}",
                    parcela = g.Sum(x => x.parcela),
                    juros = g.Sum(x => x.juros),
                    amortizacao = amort,
                    qtd = g.Count(),
                    saldoApos = Math.Round(saldo - amort, 2)
                };
                saldo = Math.Round(saldo - amort, 2);
                return linha;
            })
            .ToList();

        var contratos = itens
            .GroupBy(x => x.contrato)
            .Select(g => new
            {
                nome = g.Key,
                parcelaMensal = g.OrderBy(x => x.DataVencimento).First().parcela,
                parcelasRestantes = g.Count(),
                restante = g.Sum(x => x.parcela),
                juros = g.Sum(x => x.juros),
                principal = g.Sum(x => x.amortizacao),
                proximoVencimento = g.Min(x => x.DataVencimento),
                ultimoVencimento = g.Max(x => x.DataVencimento)
            })
            .OrderByDescending(x => x.restante)
            .ToList();

        var comprometimentoMensal = timeline.Count > 0 ? timeline[0].parcela : 0m;

        return Ok(new
        {
            resumo = new
            {
                comprometimentoMensal,
                totalRestante = itens.Sum(x => x.parcela),
                jurosRestante = itens.Sum(x => x.juros),
                principalRestante = totalPrincipal,
                parcelasRestantes = itens.Count,
                contratosAtivos = contratos.Count,
                proximoVencimento = itens.Count > 0 ? itens.Min(x => x.DataVencimento) : (DateTime?)null,
                ultimoVencimento = itens.Count > 0 ? itens.Max(x => x.DataVencimento) : (DateTime?)null
            },
            contratos,
            timeline
        });
    }

    // ── Lista de contratos cadastrados ───────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.Financiamentos.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId)
            .OrderByDescending(f => f.CriadoEm)
            .Select(f => new
            {
                f.Id, f.Banco, f.Titulo, f.Descricao, f.ValorCredito, f.ValorParcela,
                f.NumeroParcelas, f.TaxaEfetivaMensal, f.PrimeiroVencimento, f.ContratoPdfUrl
            })
            .ToListAsync(ct));

    // ── Análise do PDF do contrato (não salva nada) ──────────────────────────
    [HttpPost("analisar-pdf")]
    [RequestSizeLimit(10_000_000)]
    public IActionResult AnalisarPdf(IFormFile arquivo)
    {
        if (arquivo is null || arquivo.Length == 0) return BadRequest("Nenhum arquivo enviado.");
        if (!arquivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Envie o PDF do contrato/extrato.");

        var texto = ExtrairTexto(arquivo);
        var d = ParsearExtrato(texto);
        if (d.NumeroParcelas <= 0 || d.ValorParcela <= 0)
            return Ok(new { reconhecido = false, textoParcial = texto.Length > 1200 ? texto[..1200] : texto });

        // Crédito estimado = valor presente das parcelas na taxa nominal (o "Valor Liberado" do
        // extrato é o total do contrato, não o que entrou na conta).
        var taxa = d.TaxaNominalMensal > 0 ? d.TaxaNominalMensal
                 : AmortizacaoPrice.ResolverTaxaMensal(d.ValorLiberado, d.ValorParcela, d.NumeroParcelas);
        var creditoEstimado = ValorPresente(d.ValorParcela, taxa, d.NumeroParcelas);
        var previa = MontarPrevia(creditoEstimado, d.ValorParcela, d.NumeroParcelas, d.ParcelasPagas, d.PrimeiroVencimento);

        return Ok(new
        {
            reconhecido = true,
            banco = d.Banco,
            titulo = d.Titulo,
            valorLiberado = d.ValorLiberado,
            taxaNominalMensal = d.TaxaNominalMensal,
            numeroParcelas = d.NumeroParcelas,
            parcelasPagas = d.ParcelasPagas,
            valorParcela = d.ValorParcela,
            primeiroVencimento = d.PrimeiroVencimento,
            dataLiberacao = d.DataLiberacao,
            creditoEstimado,
            previa
        });
    }

    // ── Prévia recalculada com valores editados (não salva) ──────────────────
    public record PreviaRequest(decimal Credito, decimal ValorParcela, int NumeroParcelas,
        int ParcelasPagas, DateTime PrimeiroVencimento);

    [HttpPost("previa")]
    public IActionResult Previa([FromBody] PreviaRequest req)
        => Ok(MontarPrevia(req.Credito, req.ValorParcela, req.NumeroParcelas, req.ParcelasPagas, req.PrimeiroVencimento));

    // ── Cria o financiamento e lança as parcelas ─────────────────────────────
    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> Criar(
        [FromForm] Guid empresaId, [FromForm] string banco, [FromForm] string? titulo,
        [FromForm] decimal credito, [FromForm] decimal valorParcela, [FromForm] int numeroParcelas,
        [FromForm] int parcelasPagas, [FromForm] DateTime primeiroVencimento,
        [FromForm] DateTime? dataLiberacao, [FromForm] bool lancarEntrada,
        IFormFile? contrato, CancellationToken ct)
    {
        if (numeroParcelas <= 0 || valorParcela <= 0 || credito <= 0)
            return BadRequest("Informe crédito, valor da parcela e número de parcelas.");

        var taxa = AmortizacaoPrice.ResolverTaxaMensal(credito, valorParcela, numeroParcelas);
        var tabela = AmortizacaoPrice.Montar(credito, valorParcela, numeroParcelas, taxa);
        var restantes = tabela.Skip(Math.Max(0, parcelasPagas)).ToList();
        if (restantes.Count == 0) return BadRequest("Todas as parcelas já estariam pagas.");

        var grupo = Guid.NewGuid().ToString();
        var baseDesc = string.IsNullOrWhiteSpace(titulo) ? banco : $"{banco} {titulo}";
        var total = restantes.Count;

        // Entrada (crédito na conta) — opcional; não é receita (categoria Empréstimo Captado).
        if (lancarEntrada)
        {
            var entrada = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaReceber,
                $"{baseDesc} - crédito", credito, dataLiberacao ?? primeiroVencimento);
            entrada.DefinirClassificacao("Empréstimo Captado", banco, null);
            entrada.Baixar(credito, dataLiberacao ?? DateTime.Today);
            db.LancamentosFinanceiros.Add(entrada);
        }

        // Parcelas (uma conta a pagar por mês; ValorJuros = parte de juro embutida).
        for (int idx = 0; idx < total; idx++)
        {
            var linha = restantes[idx];
            var venc = primeiroVencimento.AddMonths(idx);
            var parc = LancamentoFinanceiro.Criar(empresaId, TipoLancamento.ContaPagar,
                $"{baseDesc} - {idx + 1}/{total}", valorParcela, venc,
                parcela: idx + 1, totalParcelas: total, grupoParcelamento: grupo);
            parc.DefinirClassificacao("Financiamentos", banco, null);
            parc.DefinirJuros(linha.Juros);
            db.LancamentosFinanceiros.Add(parc);
        }

        var fin = Financiamento.Criar(empresaId, banco, titulo, baseDesc, credito, valorParcela,
            numeroParcelas, taxa, primeiroVencimento, grupo, lancouEntrada: lancarEntrada);

        if (contrato is { Length: > 0 })
        {
            var dir = Path.Combine("wwwroot", "uploads", "financiamentos");
            Directory.CreateDirectory(dir);
            var nome = $"{fin.Id}.pdf";
            using (var stream = System.IO.File.Create(Path.Combine(dir, nome)))
                await contrato.CopyToAsync(stream, ct);
            fin.AnexarContrato($"/uploads/financiamentos/{nome}");
        }

        db.Financiamentos.Add(fin);
        await uow.SalvarAsync(ct);

        return Ok(new { fin.Id, parcelasCriadas = total, taxaEfetivaMensal = taxa, fin.ContratoPdfUrl });
    }

    // ── Anexa/atualiza o PDF do contrato de um financiamento já cadastrado ────
    [HttpPost("{id:guid}/contrato")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> AnexarContrato(Guid id, IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0) return BadRequest("Nenhum arquivo enviado.");
        if (!arquivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Envie o PDF do contrato.");

        var fin = await db.Financiamentos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Financiamento não encontrado.");

        var dir = Path.Combine("wwwroot", "uploads", "financiamentos");
        Directory.CreateDirectory(dir);
        var nome = $"{fin.Id}.pdf";
        using (var stream = System.IO.File.Create(Path.Combine(dir, nome)))
            await arquivo.CopyToAsync(stream, ct);

        var url = $"/uploads/financiamentos/{nome}";
        fin.AnexarContrato(url);
        await uow.SalvarAsync(ct);
        return Ok(new { url });
    }

    // ── Exclui o contrato e as parcelas ainda em aberto ──────────────────────
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var fin = await db.Financiamentos.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Financiamento não encontrado.");

        var parcelas = await db.LancamentosFinanceiros
            .Where(l => l.GrupoParcelamento == fin.GrupoParcelamento
                && l.Status == StatusLancamento.EmAberto)
            .ToListAsync(ct);
        db.LancamentosFinanceiros.RemoveRange(parcelas);

        if (!string.IsNullOrEmpty(fin.ContratoPdfUrl))
        {
            var caminho = Path.Combine("wwwroot", fin.ContratoPdfUrl.TrimStart('/'));
            if (System.IO.File.Exists(caminho)) System.IO.File.Delete(caminho);
        }
        db.Financiamentos.Remove(fin);
        await uow.SalvarAsync(ct);
        return Ok(new { parcelasRemovidas = parcelas.Count });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static object MontarPrevia(decimal credito, decimal parcela, int n, int pagas, DateTime primeiroVenc)
    {
        var taxa = AmortizacaoPrice.ResolverTaxaMensal(credito, parcela, n);
        var tabela = AmortizacaoPrice.Montar(credito, parcela, n, taxa);
        var restantes = tabela.Skip(Math.Max(0, pagas)).ToList();
        var linhas = restantes.Select((l, idx) => new
        {
            numero = idx + 1,
            vencimento = primeiroVenc.AddMonths(idx),
            juros = l.Juros,
            amortizacao = l.Amortizacao,
            parcela
        }).ToList();
        return new
        {
            taxaEfetivaMensal = taxa,
            parcelasRestantes = restantes.Count,
            totalRestante = restantes.Count * parcela,
            jurosRestante = restantes.Sum(l => l.Juros),
            principalRestante = restantes.Sum(l => l.Amortizacao),
            linhas
        };
    }

    private static decimal ValorPresente(decimal parcela, decimal i, int n)
        => i <= 0 ? parcela * n
                  : Math.Round(parcela * (1 - (decimal)Math.Pow(1 + (double)i, -n)) / i, 2);

    private record ExtratoInfo(string Banco, string? Titulo, decimal ValorLiberado, decimal TaxaNominalMensal,
        int NumeroParcelas, int ParcelasPagas, decimal ValorParcela, DateTime PrimeiroVencimento, DateTime? DataLiberacao);

    private static readonly Regex RxTitulo = new(@"Nro do T[íi]tulo\s+([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
    private static readonly Regex RxLiberado = new(@"Valor Liberado[^0-9]*([\d.]+,\d{2})", RegexOptions.IgnoreCase);
    private static readonly Regex RxTaxa = new(@"Taxa de Juros ao M[êe]s\s+([\d.,]+)", RegexOptions.IgnoreCase);
    private static readonly Regex RxTotParc = new(@"Total de Parcelas\s+(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex RxParcPagas = new(@"Parcelas Pagas\s+(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex RxProxVenc = new(@"Pr[óo]ximo Vencimento\s+(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
    private static readonly Regex RxLiberacao = new(@"Data de Libera[çc][ãa]o\s+(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);

    private static ExtratoInfo ParsearExtrato(string texto)
    {
        decimal Dec(string s) => decimal.Parse(s.Replace(".", "").Replace(",", "."), CultureInfo.InvariantCulture);
        DateTime? Data(string s) => DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;

        var banco = texto.Contains("SICREDI", StringComparison.OrdinalIgnoreCase) ? "SICREDI"
                  : texto.Contains("Sicredi", StringComparison.OrdinalIgnoreCase) ? "SICREDI" : "Banco";
        var titulo = RxTitulo.Match(texto) is { Success: true } mt ? mt.Groups[1].Value : null;
        var liberado = RxLiberado.Match(texto) is { Success: true } ml ? Dec(ml.Groups[1].Value) : 0m;
        var taxa = RxTaxa.Match(texto) is { Success: true } mtx ? Dec(mtx.Groups[1].Value) / 100m : 0m;
        var totParc = RxTotParc.Match(texto) is { Success: true } mtp ? int.Parse(mtp.Groups[1].Value) : 0;
        var pagas = RxParcPagas.Match(texto) is { Success: true } mpp ? int.Parse(mpp.Groups[1].Value) : 0;
        var proxVenc = RxProxVenc.Match(texto) is { Success: true } mpv ? Data(mpv.Groups[1].Value) : null;
        var liberacao = RxLiberacao.Match(texto) is { Success: true } mlb ? Data(mlb.Groups[1].Value) : null;

        var parcela = totParc > 0 && liberado > 0 ? Math.Round(liberado / totParc, 2) : 0m;
        var primeiro = proxVenc ?? DateTime.Today;

        return new ExtratoInfo(banco, titulo, liberado, taxa, totParc, pagas, parcela, primeiro, liberacao);
    }

    private static string ExtrairTexto(IFormFile arquivo)
    {
        using var stream = arquivo.OpenReadStream();
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            var words = page.GetWords().ToList();
            if (words.Count == 0) continue;
            var linhas = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                .OrderByDescending(g => g.Key)
                .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
            foreach (var linha in linhas) sb.AppendLine(linha);
        }
        return sb.ToString();
    }
}
