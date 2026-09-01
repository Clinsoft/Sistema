using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;
using PdfSharp.Pdf.IO;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;
using PdfSharpDocument = PdfSharp.Pdf.PdfDocument;

namespace Sistema.API.Controllers.Financeiro;

[ApiController]
[Route("api/contas-pagar")]
[Authorize(Roles = "Administrador,Gerente,Financeiro,Contador")]
public class ContasPagarController(
    ILancamentoFinanceiroRepository repo,
    IContaBancariaRepository contaRepo,
    SistemaDbContext db,
    IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] DateTime? inicio, [FromQuery] DateTime? fim,
        [FromQuery] string? status, CancellationToken ct)
    {
        // Sem datas → retorna TODAS (intervalo bem amplo). Com datas → filtra pelo período.
        var lancamentos = await repo.ListarPorPeriodoAsync(
            empresaId, TipoLancamento.ContaPagar,
            inicio ?? new DateTime(2000, 1, 1),
            fim ?? new DateTime(2100, 12, 31), ct);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusLancamento>(status, out var st))
            lancamentos = lancamentos.Where(l => l.Status == st);

        var lista = lancamentos.ToList();
        var fornecedorIds = lista.Where(l => l.FornecedorId.HasValue)
            .Select(l => l.FornecedorId!.Value).Distinct().ToList();
        var fornecedores = fornecedorIds.Any()
            ? await db.Fornecedores.AsNoTracking()
                .Where(f => fornecedorIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.RazaoSocial, ct)
            : new Dictionary<Guid, string>();

        var colaboradorIds = lista.Where(l => l.ColaboradorId.HasValue)
            .Select(l => l.ColaboradorId!.Value).Distinct().ToList();
        var colaboradores = colaboradorIds.Any()
            ? await db.Usuarios.AsNoTracking()
                .Where(u => colaboradorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Nome, ct)
            : new Dictionary<Guid, string>();

        return Ok(lista.Select(l => new
        {
            l.Id, l.Descricao, l.ValorOriginal, l.ValorPago,
            saldo = l.Saldo, l.DataVencimento, l.DataPagamento,
            status = l.Status.ToString(), l.Parcela, l.TotalParcelas, l.Observacao,
            categoria = l.Categoria, l.FornecedorId, l.ColaboradorId,
            fornecedorNome = l.FornecedorId.HasValue && fornecedores.TryGetValue(l.FornecedorId.Value, out var fn) ? fn
                           : l.ColaboradorId.HasValue && colaboradores.TryGetValue(l.ColaboradorId.Value, out var cn) ? cn
                           : l.ClienteNome,   // beneficiário informado manualmente (ex.: transportadora do CT-e)
            l.DocumentoOrigem, vencido = l.Vencido, l.ComprovanteUrl
        }));
    }

    [HttpGet("vencidas")]
    public async Task<IActionResult> Vencidas([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lancamentos = await repo.ListarVencidosAsync(empresaId, TipoLancamento.ContaPagar, ct);
        return Ok(new { total = lancamentos.Sum(l => l.Saldo), lancamentos });
    }

    /// <summary>
    /// Lista de beneficiários para o campo Fornecedor/Beneficiário: fornecedores
    /// e colaboradores (funcionários), já unificados e marcados com o tipo.
    /// </summary>
    [HttpGet("beneficiarios")]
    public async Task<IActionResult> Beneficiarios([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var fornecedores = await db.Fornecedores.AsNoTracking()
            .Where(f => f.EmpresaId == empresaId && f.Ativo)
            .Select(f => new { id = f.Id, nome = f.RazaoSocial, tipo = "Fornecedor", documento = f.Cnpj })
            .ToListAsync(ct);

        var colaboradores = await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Ativo)
            .Select(u => new { id = u.Id, nome = u.Nome, tipo = "Colaborador", documento = u.Cpf })
            .ToListAsync(ct);

        return Ok(fornecedores.Concat(colaboradores).OrderBy(x => x.nome));
    }

    /// <summary>
    /// Cadastro rápido de colaborador (funcionário) direto do Contas a Pagar, para
    /// lançar salário sem sair da tela. Cria só os dados básicos, sem login.
    /// </summary>
    [HttpPost("colaborador")]
    public async Task<IActionResult> CriarColaborador([FromBody] CriarColaboradorRapidoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            return BadRequest(new { mensagem = "Informe o nome do colaborador." });

        var colaborador = Sistema.Domain.Cadastros.Entities.Usuario.CriarColaborador(
            req.EmpresaId, req.Nome.Trim(), req.Cpf, req.Telefone);
        db.Usuarios.Add(colaborador);
        await uow.SalvarAsync(ct);
        return Ok(new { colaborador.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarContaPagarRequest req, CancellationToken ct)
    {
        var grupo = Guid.NewGuid().ToString();
        var ids = new List<Guid>();

        for (int i = 1; i <= req.TotalParcelas; i++)
        {
            var vencimento = req.PrimeiroVencimento.AddMonths(i - 1);
            var valorParcela = Math.Round(req.Valor / req.TotalParcelas, 2);

            var l = LancamentoFinanceiro.Criar(req.EmpresaId, TipoLancamento.ContaPagar,
                req.Descricao, valorParcela, vencimento,
                fornecedorId: req.FornecedorId, categoriaId: req.CategoriaId,
                contaBancariaId: req.ContaBancariaId,
                documentoOrigem: req.DocumentoOrigem,
                parcela: i, totalParcelas: req.TotalParcelas, grupoParcelamento: grupo,
                colaboradorId: req.ColaboradorId);

            l.DefinirClassificacao(req.Categoria, null, req.Observacao);

            await repo.AdicionarAsync(l, ct);
            ids.Add(l.Id);
        }

        await uow.SalvarAsync(ct);
        return Ok(new { grupo, qtdParcelas = req.TotalParcelas, ids });
    }

    [HttpPost("{id:guid}/pagar")]
    public async Task<IActionResult> Pagar(Guid id, [FromBody] BaixarLancamentoRequest req, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");

        lancamento.Baixar(req.ValorPago, req.DataPagamento, req.ContaBancariaId);

        if (req.ContaBancariaId.HasValue)
        {
            var conta = await contaRepo.ObterPorIdAsync(req.ContaBancariaId.Value, ct);
            if (conta is not null)
            {
                conta.Debitar(req.ValorPago);
                contaRepo.Atualizar(conta);
            }
        }

        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Estorna o pagamento de um título (volta para Em Aberto, remove o
    /// comprovante e credita de volta a conta bancária, se havia). Usado quando o
    /// pagamento foi lançado errado (ex.: comprovante trocado).</summary>
    [HttpPost("{id:guid}/estornar-pagamento")]
    [Authorize(Roles = "Administrador,Gerente,Financeiro")]
    public async Task<IActionResult> EstornarPagamento(Guid id, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");

        if (lancamento.Status != StatusLancamento.Pago && lancamento.Status != StatusLancamento.PagoParcialmente)
            return BadRequest(new { mensagem = "Só é possível estornar um título pago." });

        var (contaBancariaId, valor) = lancamento.EstornarPagamento();

        // Credita de volta na conta bancária, se o pagamento havia debitado uma.
        if (contaBancariaId.HasValue && valor > 0)
        {
            var conta = await contaRepo.ObterPorIdAsync(contaBancariaId.Value, ct);
            if (conta is not null) { conta.Creditar(valor); contaRepo.Atualizar(conta); }
        }

        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Baixa VÁRIAS contas de uma vez (ex.: um boleto do Rápido 90 que junta vários CT-e).
    /// Paga o saldo de cada título selecionado com a mesma data/conta e anexa o MESMO comprovante a todas.</summary>
    [HttpPost("pagar-lote")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> PagarLote(
        [FromForm] string ids, [FromForm] DateTime dataPagamento,
        [FromForm] Guid? contaBancariaId, IFormFile? comprovante, CancellationToken ct)
    {
        var idList = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct().ToList();
        if (idList.Count == 0) return BadRequest(new { mensagem = "Selecione ao menos uma conta." });

        // Salva o comprovante UMA vez (o boleto) e reusa a URL para todos.
        string? comprovanteUrl = null;
        if (comprovante is { Length: > 0 })
        {
            var ext = Path.GetExtension(comprovante.FileName).ToLowerInvariant();
            var permitidas = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".bmp" };
            if (!permitidas.Contains(ext)) return BadRequest(new { mensagem = "Formato não suportado (use imagem ou PDF)." });
            var dir = Path.Combine("wwwroot", "uploads", "comprovantes");
            Directory.CreateDirectory(dir);
            var nome = $"lote-{Guid.NewGuid():N}{ext}";
            using (var s = System.IO.File.Create(Path.Combine(dir, nome))) await comprovante.CopyToAsync(s, ct);
            comprovanteUrl = $"/uploads/comprovantes/{nome}";
        }

        decimal totalPago = 0m; var pagos = new List<Guid>();
        foreach (var id in idList)
        {
            var l = await repo.ObterPorIdAsync(id, ct);
            if (l is null || l.Status == StatusLancamento.Pago || l.Status == StatusLancamento.Cancelado) continue;
            var saldo = l.ValorOriginal - l.ValorPago;
            if (saldo <= 0) continue;
            l.Baixar(saldo, dataPagamento, contaBancariaId);
            if (comprovanteUrl is not null) l.AnexarComprovante(comprovanteUrl);
            repo.Atualizar(l);
            totalPago += saldo; pagos.Add(id);
        }

        // Debita a conta bancária uma vez, pelo total.
        if (contaBancariaId.HasValue && totalPago > 0)
        {
            var conta = await contaRepo.ObterPorIdAsync(contaBancariaId.Value, ct);
            if (conta is not null) { conta.Debitar(totalPago); contaRepo.Atualizar(conta); }
        }

        await uow.SalvarAsync(ct);
        return Ok(new { pagas = pagos.Count, totalPago, comprovanteUrl });
    }

    /// <summary>Anexa um comprovante (imagem ou PDF) direto ao lançamento, só para guardar —
    /// NÃO lê/parseia nada. Substitui o comprovante anterior, se houver.</summary>
    [HttpPost("{id:guid}/comprovante")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> AnexarComprovanteArquivo(Guid id, [FromForm] IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0) return BadRequest("Arquivo vazio.");
        var lancamento = await repo.ObterPorIdAsync(id, ct);
        if (lancamento is null) return NotFound();

        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        var permitidas = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".bmp" };
        if (!permitidas.Contains(ext)) return BadRequest("Formato não suportado (use imagem ou PDF).");

        var dir = Path.Combine("wwwroot", "uploads", "comprovantes");
        Directory.CreateDirectory(dir);
        var nome = $"{Guid.NewGuid()}{ext}";
        using (var s = System.IO.File.Create(Path.Combine(dir, nome))) await arquivo.CopyToAsync(s, ct);

        lancamento.AnexarComprovante($"/uploads/comprovantes/{nome}");
        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return Ok(new { comprovanteUrl = lancamento.ComprovanteUrl });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] EditarLancamentoRequest req, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        lancamento.Editar(req.Descricao, req.ValorOriginal, req.DataVencimento, req.Observacao,
            req.FornecedorId, req.ColaboradorId);
        lancamento.DefinirClassificacao(req.Categoria, lancamento.ClienteNome, req.Observacao);
        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/renegociar")]
    public async Task<IActionResult> Renegociar(Guid id, [FromBody] RenegociarPagarRequest req, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        lancamento.Renegociar(req.NovoValor, req.NovoVencimento, req.Motivo);
        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
    {
        var lancamento = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Lançamento não encontrado.");
        lancamento.Cancelar();
        repo.Atualizar(lancamento);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Lê os PDFs de comprovantes de pagamento (upload), extrai valor/data/beneficiário
    /// e PROPÕE o pareamento com as contas a pagar em aberto — sem dar baixa ainda.
    /// Cada PDF é guardado; a baixa só ocorre na confirmação.
    /// </summary>
    [HttpPost("comprovantes/analisar")]
    [RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> AnalisarComprovantes([FromForm] Guid empresaId,
        [FromForm] List<IFormFile> arquivos, CancellationToken ct)
    {
        if (arquivos is null || arquivos.Count == 0) return BadRequest("Nenhum arquivo enviado.");

        var todas = await repo.ListarPorPeriodoAsync(empresaId, TipoLancamento.ContaPagar,
            new DateTime(2000, 1, 1), new DateTime(2100, 12, 31), ct);
        var candidatas = todas.Where(l => l.Status == StatusLancamento.EmAberto
            || l.Status == StatusLancamento.PagoParcialmente).ToList();

        var fornIds = candidatas.Where(l => l.FornecedorId.HasValue).Select(l => l.FornecedorId!.Value).Distinct().ToList();
        var forns = await db.Fornecedores.AsNoTracking().Where(f => fornIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => new { f.RazaoSocial, f.Cnpj }, ct);
        var colabIds = candidatas.Where(l => l.ColaboradorId.HasValue).Select(l => l.ColaboradorId!.Value).Distinct().ToList();
        var colabs = await db.Usuarios.AsNoTracking().Where(u => colabIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, ct);

        string NomeBenef(LancamentoFinanceiro l) =>
            l.FornecedorId.HasValue && forns.TryGetValue(l.FornecedorId.Value, out var f) ? f.RazaoSocial
            : l.ColaboradorId.HasValue && colabs.TryGetValue(l.ColaboradorId.Value, out var cn) ? cn : "";
        string DocBenef(LancamentoFinanceiro l) =>
            l.FornecedorId.HasValue && forns.TryGetValue(l.FornecedorId.Value, out var f) ? (f.Cnpj ?? "") : "";

        var dir = Path.Combine("wwwroot", "uploads", "comprovantes");
        Directory.CreateDirectory(dir);

        var resultado = new List<object>();
        foreach (var arq in arquivos)
        {
            if (arq.Length == 0) continue;
            var id = Guid.NewGuid();
            var caminho = Path.Combine(dir, $"{id}.pdf");
            using (var s = System.IO.File.Create(caminho)) await arq.CopyToAsync(s, ct);
            var url = $"/uploads/comprovantes/{id}.pdf";

            // Um PDF pode conter VÁRIOS pagamentos (um por página — ex.: Sicredi
            // "Pagar Boletos Eletrônicos"). Trata cada página como um comprovante.
            var paginas = ExtrairPaginas(caminho);
            if (paginas.Count == 0) paginas.Add("");
            var multi = paginas.Count > 1;
            // Fatia o PDF em 1 página por comprovante para cada conta ficar com o SEU arquivo.
            var urlsPagina = multi ? SplitPdfPorPagina(caminho, dir, id, paginas.Count) : null;

            for (var pg = 0; pg < paginas.Count; pg++)
            {
                var (valor, data, venc, nomes, docs) = ParsePagina(paginas[pg]);
                if (valor is null && nomes.Count == 0) continue; // página sem pagamento (capa, rodapé)

                var urlPg = urlsPagina != null ? urlsPagina[pg]
                          : (multi ? $"{url}#page={pg + 1}" : url);
                var benefTokens = nomes.SelectMany(Tokens).Distinct().ToList();

                var ranqueadas = candidatas.Select(l =>
                {
                    double score = 0;
                    bool valorExato = false;
                    if (valor.HasValue)
                    {
                        var alvo = l.Saldo > 0 ? l.Saldo : l.ValorOriginal;
                        if (Math.Abs(alvo - valor.Value) <= 0.01m) { score += 100; valorExato = true; }
                        else if (alvo > 0 && Math.Abs(alvo - valor.Value) <= alvo * 0.02m) score += 60;
                    }
                    var docConta = SomenteDigitos(DocBenef(l));
                    bool docBate = docConta.Length >= 11 && docs.Contains(docConta);
                    if (docBate) score += 60;
                    var nome = NomeBenef(l);
                    int nomeOverlap = 0;
                    if (benefTokens.Count > 0 && !string.IsNullOrWhiteSpace(nome))
                    {
                        var nt = Tokens(nome).ToHashSet();
                        nomeOverlap = benefTokens.Count(t => nt.Contains(t));
                        if (nomeOverlap > 0) score += Math.Min(45, nomeOverlap * 15);
                    }
                    var descTokens = Tokens(l.Descricao).ToHashSet();
                    var descOverlap = benefTokens.Count(t => descTokens.Contains(t));
                    if (descOverlap > 0) score += Math.Min(20, descOverlap * 8);
                    if (venc.HasValue && l.DataVencimento.Date == venc.Value.Date) score += 12;
                    // Confiança ALTA só quando o valor é IGUAL e o beneficiário/CNPJ corrobora
                    // (evita casar 648 com 660 só por proximidade, como o POURA x NATURAL).
                    bool confiancaAlta = valorExato && (docBate || nomeOverlap > 0);
                    return new { l, nome, score, valorExato, confiancaAlta };
                })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score).ToList();

                var melhor = ranqueadas.FirstOrDefault();
                resultado.Add(new
                {
                    arquivo = multi ? $"{arq.FileName} (pág. {pg + 1})" : arq.FileName,
                    comprovanteUrl = urlPg,
                    valorLido = valor,
                    dataLida = data,
                    vencimentoLido = venc,
                    beneficiarioLido = nomes.FirstOrDefault(),
                    documentoLido = docs.FirstOrDefault(),
                    sugestao = melhor is null ? null : (object)new
                    {
                        lancamentoId = melhor.l.Id, descricao = melhor.l.Descricao, beneficiario = melhor.nome,
                        valorOriginal = melhor.l.ValorOriginal, saldo = melhor.l.Saldo,
                        vencimento = melhor.l.DataVencimento, score = melhor.score,
                        confiancaAlta = melhor.confiancaAlta, valorExato = melhor.valorExato
                    },
                    candidatos = ranqueadas.Take(6).Select(x => new
                    {
                        lancamentoId = x.l.Id, descricao = x.l.Descricao, beneficiario = x.nome,
                        valorOriginal = x.l.ValorOriginal, saldo = x.l.Saldo,
                        vencimento = x.l.DataVencimento, score = x.score
                    })
                });
            }
        }
        return Ok(resultado);
    }

    /// <summary>Confere e dá baixa nas contas pareadas, anexando cada comprovante.</summary>
    [HttpPost("comprovantes/confirmar")]
    public async Task<IActionResult> ConfirmarComprovantes([FromBody] ConfirmarComprovantesRequest req, CancellationToken ct)
    {
        if (req?.Itens is null || req.Itens.Count == 0) return BadRequest("Nada a confirmar.");
        var baixados = 0;
        var criados = 0;
        foreach (var it in req.Itens)
        {
            var dataPg = it.DataPagamento ?? DateTime.Today;
            LancamentoFinanceiro? l;

            if (it.Criar)
            {
                // Comprovante de uma conta ainda NÃO cadastrada: cria já paga.
                var venc = it.Vencimento ?? dataPg;
                var desc = string.IsNullOrWhiteSpace(it.Descricao) ? "Pagamento (comprovante)" : it.Descricao!.Trim();
                if (desc.Length > 200) desc = desc[..200];   // coluna Descricao = 200 chars
                l = LancamentoFinanceiro.Criar(req.EmpresaId, TipoLancamento.ContaPagar, desc, it.ValorPago, venc);
                var categoria = string.IsNullOrWhiteSpace(it.Categoria) ? "Despesas Variáveis" : it.Categoria!;
                l.DefinirClassificacao(categoria, null, "Criada pela importação de comprovante.");
                await repo.AdicionarAsync(l, ct);
                criados++;
            }
            else
            {
                l = await repo.ObterPorIdAsync(it.LancamentoId, ct);
                if (l is null || l.Status == StatusLancamento.Pago) continue;
            }

            var valor = it.ValorPago > 0 ? it.ValorPago : l.Saldo;
            l.Baixar(valor, dataPg, it.ContaBancariaId);
            if (!string.IsNullOrWhiteSpace(it.ComprovanteUrl)) l.AnexarComprovante(it.ComprovanteUrl);
            if (it.ContaBancariaId.HasValue)
            {
                var conta = await contaRepo.ObterPorIdAsync(it.ContaBancariaId.Value, ct);
                if (conta is not null) { conta.Debitar(valor); contaRepo.Atualizar(conta); }
            }
            repo.Atualizar(l);
            baixados++;
        }
        await uow.SalvarAsync(ct);
        return Ok(new { baixados, criados });
    }

    /// <summary>Fatia um PDF de N páginas em N PDFs de 1 página cada (um por comprovante).
    /// Retorna as URLs na mesma ordem das páginas, ou null se não conseguir fatiar.</summary>
    private static List<string>? SplitPdfPorPagina(string caminho, string dir, Guid id, int esperado)
    {
        try
        {
            using var input = PdfReader.Open(caminho, PdfDocumentOpenMode.Import);
            if (input.PageCount != esperado) return null;
            var urls = new List<string>();
            for (var i = 0; i < input.PageCount; i++)
            {
                using var outDoc = new PdfSharpDocument();
                outDoc.AddPage(input.Pages[i]);
                var nome = $"{id}_p{i + 1}.pdf";
                outDoc.Save(Path.Combine(dir, nome));
                urls.Add($"/uploads/comprovantes/{nome}");
            }
            return urls;
        }
        catch { return null; }
    }

    // ─── Leitura/parse de comprovantes ──────────────────────────────────────
    private static List<string> ExtrairPaginas(string caminho)
    {
        var paginas = new List<string>();
        try { using var pdf = PdfPigDocument.Open(caminho); foreach (var p in pdf.GetPages()) paginas.Add(p.Text); }
        catch { /* PDF ilegível (imagem/escaneado) */ }
        return paginas;
    }

    private static string? Campo(string t, string pattern)
    {
        var m = Regex.Match(t, pattern, RegexOptions.IgnoreCase);
        return m.Success ? Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim() : null;
    }

    private static DateTime? ParseData(string? s)
        => DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Lê UM comprovante (uma página). Reconhece o layout rotulado do Sicredi
    /// (Valor Pago / Beneficiário / Beneficiário Final) e cai no parser genérico se não achar.</summary>
    private static (decimal? valor, DateTime? data, DateTime? venc, List<string> nomes, List<string> docs) ParsePagina(string texto)
    {
        var t = (texto ?? "").Replace('\n', ' ').Replace('\r', ' ');

        var valor = ParseMoeda(Campo(t, @"Valor Pago\s*\(R\$\)\s*:?\s*([\d\.]+,\d{2})"))
                 ?? ParseMoeda(Campo(t, @"Valor do T[ií]tulo\s*\(R\$\)\s*:?\s*([\d\.]+,\d{2})"))
                 ?? ParseMoeda(Campo(t, @"Valor Total\s*\(R\$\)\s*:?\s*([\d\.]+,\d{2})"));   // tributos Sicredi (DARE/DAS)
        var data = ParseData(Campo(t, @"Data do Pagamento\s*:?\s*(\d{2}/\d{2}/\d{4})")
                          ?? Campo(t, @"Data da Transa[cç][aã]o\s*:?\s*(\d{2}/\d{2}/\d{4})"));
        var venc = ParseData(Campo(t, @"Data de Vencimento\s*:?\s*(\d{2}/\d{2}/\d{4})"));

        // Fronteira do valor: o próximo rótulo pode vir SEM espaço (o PdfPig concatena
        // "...SACPF/CNPJ..."), então não exigimos \s. Limite de 80 chars por segurança.
        const string fim = @"(?=CPF/CNPJ|Nome Fantasia|Nome do|N[uú]mero|Raz[aã]o|Linha|Data|Valor|Institui|Hora|$)";
        var nomes = new List<string>();
        void AddNome(string? n) { if (!string.IsNullOrWhiteSpace(n)) nomes.Add(n!); }
        AddNome(Campo(t, @"Nome do Benefici[aá]rio Final\s*:?\s*(.{3,80}?)" + fim));
        AddNome(Campo(t, @"Raz[aã]o Social do Benefici[aá]rio\s*:?\s*(.{3,80}?)" + fim));
        AddNome(Campo(t, @"Nome Fantasia do Benefici[aá]rio\s*:?\s*(.{3,80}?)" + fim));
        // Tributos (Sicredi): o "beneficiário" vem em "Nome da Empresa" (ex.: SEFAZ SP - DARE).
        AddNome(Campo(t, @"Nome da Empresa\s*:?\s*(.{3,60}?)(?=C[oó]digo|Data|Valor|Tipo|Hora|N[uú]mero|$)"));
        // DAS do Simples Nacional não traz "Nome da Empresa" — identifica pelo cabeçalho.
        if (nomes.Count == 0 && Regex.IsMatch(t, "SIMPLES NACIONAL", RegexOptions.IgnoreCase))
            nomes.Add("DAS - Simples Nacional");

        var docs = new List<string>();
        void AddDoc(string? d) { var dd = SomenteDigitos(d); if (dd.Length is 11 or 14) docs.Add(dd); }
        AddDoc(Campo(t, @"CPF/CNPJ do Benefici[aá]rio Final\s*:?\s*([\d\.\/-]{11,20})"));
        AddDoc(Campo(t, @"CPF/CNPJ do Benefici[aá]rio\s*:?\s*([\d\.\/-]{11,20})"));

        // Fallback genérico (bancos sem os rótulos do Sicredi: PIX, TED, DARF…)
        if (valor is null)
        {
            var (v, d, b, doc) = ParseComprovante(t);
            valor = v; data ??= d;
            if (!string.IsNullOrWhiteSpace(b)) nomes.Add(b!);
            var dd = SomenteDigitos(doc); if (dd.Length is 11 or 14) docs.Add(dd);
        }
        return (valor, data, venc, nomes.Distinct().ToList(), docs.Distinct().ToList());
    }

    private static (decimal? valor, DateTime? data, string? beneficiario, string? doc) ParseComprovante(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null, null, null);
        var t = texto.Replace('\n', ' ').Replace('\r', ' ');

        decimal? valor = null;
        var mLbl = Regex.Match(t, @"valor[^\d]{0,20}R?\$?\s*([\d\.]+,\d{2})", RegexOptions.IgnoreCase);
        if (mLbl.Success) valor = ParseMoeda(mLbl.Groups[1].Value);
        if (valor is null)
        {
            var todos = Regex.Matches(t, @"R\$\s*([\d\.]+,\d{2})")
                .Select(m => ParseMoeda(m.Groups[1].Value)).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (todos.Count > 0) valor = todos.Max();
        }

        DateTime? data = null;
        var mData = Regex.Match(t, @"\d{2}/\d{2}/\d{4}");
        if (mData.Success && DateTime.TryParseExact(mData.Value, "dd/MM/yyyy",
            CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var d)) data = d;

        string? doc = null;
        var mCnpj = Regex.Match(t, @"\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}");
        if (mCnpj.Success) doc = mCnpj.Value;
        else { var mCpf = Regex.Match(t, @"\d{3}\.?\d{3}\.?\d{3}-?\d{2}"); if (mCpf.Success) doc = mCpf.Value; }

        string? benef = null;
        var mBen = Regex.Match(t,
            @"(?:benefici[aá]rio|favorecido|recebedor|quem recebeu|nome do recebedor)\s*:?\s*([A-Za-zÀ-ÿ0-9\.\-\& ]{3,60})",
            RegexOptions.IgnoreCase);
        if (mBen.Success) benef = mBen.Groups[1].Value.Trim();

        return (valor, data, benef, doc);
    }

    private static decimal? ParseMoeda(string s)
        => decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var v) ? v : null;

    private static string SomenteDigitos(string? s) => string.IsNullOrEmpty(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

    private static readonly HashSet<string> _stop = new(StringComparer.OrdinalIgnoreCase)
        { "LTDA", "EPP", "EIRELI", "COMERCIO", "COMERCIAL", "INDUSTRIA", "SERVICOS", "PAGAMENTO" };
    private static List<string> Tokens(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new List<string>();
        return Regex.Split(RemoverAcentos(s).ToUpperInvariant(), @"[^A-Z0-9]+")
            .Where(x => x.Length >= 3 && !_stop.Contains(x)).Distinct().ToList();
    }
    private static string RemoverAcentos(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

public record EditarLancamentoRequest(string Descricao, decimal ValorOriginal, DateTime DataVencimento, string? Observacao = null, Guid? FornecedorId = null, string? Categoria = null, Guid? ColaboradorId = null);
public record RenegociarPagarRequest(decimal NovoValor, DateTime NovoVencimento, string? Motivo = null);
public record ConfirmarComprovantesRequest(Guid EmpresaId, List<ConfirmarComprovanteItem> Itens);
public record ConfirmarComprovanteItem(Guid LancamentoId, decimal ValorPago, DateTime? DataPagamento,
    string? ComprovanteUrl, Guid? ContaBancariaId = null,
    bool Criar = false, string? Descricao = null, DateTime? Vencimento = null, string? Categoria = null);
public record CriarColaboradorRapidoRequest(Guid EmpresaId, string Nome, string? Cpf = null, string? Telefone = null);

public record CriarContaPagarRequest(
    Guid EmpresaId, string Descricao, decimal Valor,
    DateTime PrimeiroVencimento, int TotalParcelas = 1,
    Guid? FornecedorId = null, Guid? CategoriaId = null,
    Guid? ContaBancariaId = null, string? DocumentoOrigem = null,
    string? Categoria = null, string? Observacao = null,
    Guid? ColaboradorId = null);
