using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/entradas")]
[Authorize]
public class EntradaNFeController(SistemaDbContext db) : ControllerBase
{
    // ──────────────────────────────────────────────────────────────────
    // LISTAGEM
    // ──────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] DateTime? dataInicio, [FromQuery] DateTime? dataFim,
        [FromQuery] StatusEntradaNFe? status, CancellationToken ct)
    {
        var q = db.EntradasNFe.AsNoTracking().Where(e => e.EmpresaId == empresaId);
        if (dataInicio.HasValue) q = q.Where(e => e.DataEmissao >= dataInicio.Value);
        if (dataFim.HasValue) q = q.Where(e => e.DataEmissao <= dataFim.Value.AddDays(1));
        if (status.HasValue) q = q.Where(e => e.Status == status.Value);

        var lista = await q.OrderByDescending(e => e.DataEntrada)
            .Select(e => new
            {
                e.Id, e.ChaveAcesso, e.EmitenteNome, e.EmitenteCnpj,
                e.DataEmissao, e.DataEntrada, e.ValorTotal,
                Status = e.Status.ToString(), e.DataProcessamento,
                TotalItens = e.Itens.Count,
            }).ToListAsync(ct);

        return Ok(lista);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.AsNoTracking()
            .Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entrada is null) return NotFound();

        return Ok(new
        {
            entrada.Id,
            entrada.EmpresaId,
            entrada.NotaFiscalRecebidaId,
            entrada.ChaveAcesso,
            entrada.EmitenteNome,
            entrada.EmitenteCnpj,
            entrada.DataEmissao,
            entrada.DataEntrada,
            entrada.DataProcessamento,
            entrada.LocalEstoqueId,
            entrada.FornecedorId,
            entrada.PedidoCompraId,
            entrada.NaturezaOperacao,
            Status = entrada.Status.ToString(),
            entrada.ValorProdutos,
            entrada.ValorFrete,
            entrada.ValorFreteManual,
            entrada.ValorSeguro,
            entrada.ValorDesconto,
            entrada.ValorIpi,
            entrada.ValorIcmsSt,
            entrada.ValorTotal,
            FreteTotal = entrada.FreteTotal,
            Duplicatas = string.IsNullOrEmpty(entrada.DuplicatasJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<object>(entrada.DuplicatasJson),
            Itens = entrada.Itens.Select(i => new
            {
                i.Id,
                i.EntradaNFeId,
                i.NumeroItem,
                i.CfopXml,
                i.CfopUtilizado,
                i.NcmXml,
                i.DescricaoXml,
                i.QuantidadeXml,
                i.UnidadeXml,
                i.ValorUnitarioXml,
                i.ValorTotalXml,
                i.CodigoFornecedor,
                i.CodigoBarras,
                i.ValorIpi,
                i.ValorIcmsSt,
                i.ProdutoId,
                i.ProdutoDescricao,
                i.FatorConversao,
                i.UnidadeEstoque,
                i.QuantidadeEstoque,
                i.NumeroLote,
                i.LoteId,
                i.Validade,
                i.Tags,
                i.CustoUnitarioFinal,
                i.ValorFreteProporcional,
                i.PrecoVendaSugerido,
                i.MarkupSugerido,
                i.EstoqueMovimentado,
            }).OrderBy(i => i.NumeroItem).ToList()
        });
    }

    // ──────────────────────────────────────────────────────────────────
    // IMPORTAR XML DIRETAMENTE (upload de arquivo .xml)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Importa um XML de NF-e (arquivo ou corpo da requisição).
    /// Cria NotaFiscalRecebida + EntradaNFe em uma única operação.
    /// Extrai: emitente, totais, frete, IBS/CBS, duplicatas, itens com NCM/CFOP.
    /// Tenta vincular produtos por CodigoFornecedor (cProd) ou CodigoBarras (cEAN).
    /// </summary>
    [HttpPost("importar-xml")]
    public async Task<IActionResult> ImportarXml(
        [FromQuery] Guid empresaId,
        [FromQuery] Guid localEstoqueId,
        [FromQuery] decimal freteManual = 0,
        IFormFile? arquivo = null,
        CancellationToken ct = default)
    {
        // Lê o XML — aceita upload de arquivo ou body raw
        string xml;
        if (arquivo is not null)
        {
            using var sr = new System.IO.StreamReader(arquivo.OpenReadStream());
            xml = await sr.ReadToEndAsync(ct);
        }
        else
        {
            Request.EnableBuffering();
            using var sr = new System.IO.StreamReader(Request.Body);
            xml = await sr.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(xml))
            return BadRequest(new { mensagem = "XML não informado." });

        // Verificar local de estoque
        var local = await db.LocaisEstoque
            .FirstOrDefaultAsync(l => l.Id == localEstoqueId && l.EmpresaId == empresaId, ct);
        if (local is null)
            return BadRequest(new { mensagem = "Local de estoque não encontrado." });

        NFeParseResult parsed;
        try { parsed = ParsearNFeXml(xml); }
        catch (Exception ex) { return BadRequest(new { mensagem = $"Erro ao interpretar XML: {ex.Message}" }); }

        // Evitar duplicata
        var entradaExistente = await db.EntradasNFe
            .Where(e => e.ChaveAcesso == parsed.ChaveAcesso && e.EmpresaId == empresaId)
            .Select(e => new { e.Id, e.Status })
            .FirstOrDefaultAsync(ct);
        if (entradaExistente is not null)
            return Conflict(new {
                mensagem = $"NF-e {parsed.NumeroNF} já importada.",
                entradaId = entradaExistente.Id,
                status = entradaExistente.Status.ToString()
            });

        // Criar ou reusar NotaFiscalRecebida
        var notaRecebida = await db.NotasFiscaisRecebidas
            .FirstOrDefaultAsync(n => n.ChaveAcesso == parsed.ChaveAcesso && n.EmpresaId == empresaId, ct);

        if (notaRecebida is null)
        {
            notaRecebida = NotaFiscalRecebida.Criar(
                empresaId, parsed.ChaveAcesso,
                nsu: "0", modelo: parsed.Modelo, serie: parsed.Serie,
                numero: parsed.NumeroNF, dataEmissao: parsed.DataEmissao,
                emitenteCnpj: parsed.EmitenteCnpj, emitenteNome: parsed.EmitenteNome,
                emitenteUF: parsed.EmitenteUF, valorTotal: parsed.ValorTotal,
                situacao: SituacaoNFeRecebida.Autorizada);
            notaRecebida.SalvarXml(xml);
            db.NotasFiscaisRecebidas.Add(notaRecebida);
            await db.SaveChangesAsync(ct);
        }

        // Criar EntradaNFe
        var entrada = EntradaNFe.Criar(
            empresaId, notaRecebida.Id, parsed.ChaveAcesso,
            parsed.EmitenteNome, parsed.EmitenteCnpj, parsed.DataEmissao,
            localEstoqueId,
            valProdutos: parsed.ValorProdutos,
            valFrete: parsed.ValorFrete,
            valSeguro: parsed.ValorSeguro,
            valDesconto: parsed.ValorDesconto,
            valIpi: parsed.ValorIpi,
            valIcmsSt: parsed.ValorIcmsSt,
            valTotal: parsed.ValorTotal,
            natureza: parsed.NaturezaOperacao);

        // Vincular ou criar fornecedor automaticamente pelo CNPJ do emitente
        var cnpjForn = CnpjRaw(parsed.EmitenteCnpj);
        var fornecedor = await db.Fornecedores.FirstOrDefaultAsync(
            f => f.EmpresaId == empresaId && f.Cnpj == cnpjForn, ct);

        if (fornecedor is null && !string.IsNullOrWhiteSpace(cnpjForn))
        {
            fornecedor = Fornecedor.Criar(
                empresaId,
                razaoSocial: parsed.EmitenteNome,
                cnpj: cnpjForn,
                nomeFantasia: parsed.EmitenteNomeFantasia);

            // Endereço completo do XML
            if (parsed.EmitenteEndereco is { } end)
                fornecedor.Editar(
                    razaoSocial: parsed.EmitenteNome,
                    nomeFantasia: parsed.EmitenteNomeFantasia,
                    email: null, telefone: null, contato: null, prazoPagamentoDias: 0,
                    logradouro: end.Logradouro, numero: end.Numero, complemento: end.Complemento,
                    bairro: end.Bairro, cidade: end.Municipio, uf: end.UF, cep: end.Cep,
                    inscricaoEstadual: parsed.EmitenteIE);

            db.Fornecedores.Add(fornecedor);
        }

        if (fornecedor is not null)
            entrada.VincularFornecedor(fornecedor.Id);

        // Adicionar itens parseados
        foreach (var itemParsed in parsed.Itens)
            entrada.AdicionarItem(itemParsed);

        if (freteManual > 0)
            entrada.DefinirFreteManual(freteManual);

        if (parsed.Duplicatas.Count > 0)
        {
            var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
            entrada.DefinirDuplicatas(System.Text.Json.JsonSerializer.Serialize(parsed.Duplicatas, jsonOpts));
        }

        db.EntradasNFe.Add(entrada);
        await db.SaveChangesAsync(ct);

        // Tentar vincular produtos automaticamente (por cProd ou cEAN)
        var itens = await db.EntradasNFe.Include(e => e.Itens)
            .Where(e => e.Id == entrada.Id)
            .Select(e => e.Itens)
            .FirstOrDefaultAsync(ct) ?? [];

        var codsFornecedor = parsed.Itens.Where(i => i.CodigoFornecedor != null)
            .Select(i => i.CodigoFornecedor!).ToList();
        var codsBarras = parsed.Itens.Where(i => i.CodigoBarras != null)
            .Select(i => i.CodigoBarras!).ToList();

        var produtosPorCodForn = await db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && codsFornecedor.Contains(p.Codigo))
            .ToDictionaryAsync(p => p.Codigo, ct);

        // De-para: produtos que já memorizaram o código deste emitente em entradas anteriores.
        var cnpjEmitente = CnpjRaw(parsed.EmitenteCnpj);
        var fornecedorEntrada = await db.Fornecedores.AsNoTracking()
            .FirstOrDefaultAsync(f => f.EmpresaId == empresaId && f.Cnpj == cnpjEmitente, ct);
        var produtosPorDePara = fornecedorEntrada is not null && codsFornecedor.Any()
            ? await db.Produtos.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId
                    && p.FornecedorPrincipalId == fornecedorEntrada.Id
                    && p.CodigoFornecedorPrincipal != null
                    && codsFornecedor.Contains(p.CodigoFornecedorPrincipal))
                .ToDictionaryAsync(p => p.CodigoFornecedorPrincipal!, ct)
            : new Dictionary<string, Domain.Estoque.Entities.Produto>();

        var produtosPorBarras = codsBarras.Any()
            ? await db.Produtos.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId && codsBarras.Contains(p.CodigoBarras!))
                .ToDictionaryAsync(p => p.CodigoBarras!, ct)
            : new Dictionary<string, Domain.Estoque.Entities.Produto>();

        int vinculados = 0;
        foreach (var item in itens)
        {
            Domain.Estoque.Entities.Produto? prod = null;
            if (item.CodigoBarras != null && produtosPorBarras.TryGetValue(item.CodigoBarras, out var pBarras))
                prod = pBarras;
            else if (item.CodigoFornecedor != null && produtosPorDePara.TryGetValue(item.CodigoFornecedor, out var pDePara))
                prod = pDePara;
            else if (item.CodigoFornecedor != null && produtosPorCodForn.TryGetValue(item.CodigoFornecedor, out var pCod))
                prod = pCod;

            if (prod is not null)
            {
                item.VincularProduto(prod.Id, prod.Descricao);
                vinculados++;
            }
        }

        if (vinculados > 0)
            await db.SaveChangesAsync(ct);

        return Ok(new
        {
            id = entrada.Id,
            numeroNF = parsed.NumeroNF,
            emitente = parsed.EmitenteNome,
            valorTotal = parsed.ValorTotal,
            totalItens = parsed.Itens.Count,
            itensVinculados = vinculados,
            itensPendentes = parsed.Itens.Count - vinculados,
            duplicatas = parsed.Duplicatas,
            avisos = parsed.Avisos,
            mensagem = vinculados == parsed.Itens.Count
                ? "XML importado. Todos os produtos foram vinculados. Revise e processe."
                : $"XML importado. {parsed.Itens.Count - vinculados} produto(s) precisam ser vinculados manualmente antes de processar.",
        });
    }

    // ──────────────────────────────────────────────────────────────────
    // CRIAR ENTRADA A PARTIR DE NF-e RECEBIDA (parse do XML)
    // ──────────────────────────────────────────────────────────────────

    [HttpPost("de-nfe-recebida/{notaId:guid}")]
    public async Task<IActionResult> CriarDeNFeRecebida(
        Guid notaId, [FromBody] IniciarEntradaRequest req, CancellationToken ct)
    {
        var nota = await db.NotasFiscaisRecebidas
            .FirstOrDefaultAsync(n => n.Id == notaId && n.EmpresaId == req.EmpresaId, ct)
            ?? throw new KeyNotFoundException("NF-e não encontrada.");

        // Verificar se já existe entrada para esta nota
        if (await db.EntradasNFe.AnyAsync(e => e.NotaFiscalRecebidaId == notaId, ct))
            return Conflict(new { mensagem = "Já existe uma escrituração para esta NF-e." });

        // Verificar local de estoque
        var local = await db.LocaisEstoque
            .FirstOrDefaultAsync(l => l.Id == req.LocalEstoqueId && l.EmpresaId == req.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Local de estoque não encontrado.");

        // Criar entrada com dados da nota recebida
        var entrada = EntradaNFe.Criar(
            req.EmpresaId, notaId, nota.ChaveAcesso,
            nota.EmitenteNome, nota.EmitenteCnpj, nota.DataEmissao,
            req.LocalEstoqueId,
            valProdutos: nota.ValorTotal, valFrete: 0, valSeguro: 0,
            valDesconto: 0, valIpi: 0, valIcmsSt: 0, valTotal: nota.ValorTotal);

        // Se XML disponível, parsear itens E duplicatas (parcelamento da NF-e)
        if (nota.XmlNota is not null)
        {
            try
            {
                var parsed = ParsearNFeXml(nota.XmlNota);
                foreach (var item in parsed.Itens)
                    entrada.AdicionarItem(item);

                // Duplicatas/parcelas do XML → alimentam as contas a pagar na escrituração
                if (parsed.Duplicatas.Count > 0)
                {
                    var jsonOpts = new System.Text.Json.JsonSerializerOptions
                    { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
                    entrada.DefinirDuplicatas(System.Text.Json.JsonSerializer.Serialize(parsed.Duplicatas, jsonOpts));
                }
            }
            catch { /* XML inválido → segue sem itens/duplicatas */ }
        }

        // Tentar vincular fornecedor por CNPJ
        var fornecedor = await db.Fornecedores
            .FirstOrDefaultAsync(f => f.EmpresaId == req.EmpresaId &&
                f.Cnpj == CnpjRaw(nota.EmitenteCnpj), ct);
        if (fornecedor is not null)
            entrada.VincularFornecedor(fornecedor.Id);

        // Vincula automaticamente aos produtos já cadastrados
        // (código de barras → de-para do fornecedor → código interno)
        await VincularProdutosAutomaticamenteAsync(entrada, ct);

        db.EntradasNFe.Add(entrada);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obter), new { id = entrada.Id }, new { id = entrada.Id });
    }

    // ──────────────────────────────────────────────────────────────────
    // VINCULAR FORNECEDOR
    // ──────────────────────────────────────────────────────────────────

    [HttpPatch("{id:guid}/fornecedor")]
    public async Task<IActionResult> VincularFornecedor(
        Guid id, [FromBody] VincularFornecedorRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");
        entrada.VincularFornecedor(req.FornecedorId);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ──────────────────────────────────────────────────────────────────
    // EDITAR ITEM (CFOP, conversão, lote/validade, tags, preço)
    // ──────────────────────────────────────────────────────────────────

    [HttpPatch("{id:guid}/itens/{itemId:guid}")]
    public async Task<IActionResult> EditarItem(
        Guid id, Guid itemId, [FromBody] EditarItemRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.Status == StatusEntradaNFe.Processada)
            return BadRequest(new { mensagem = "Entrada já processada. Estorne antes de editar." });

        var item = entrada.Itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Item não encontrado.");

        if (req.CfopUtilizado is not null) item.DefinirCfop(req.CfopUtilizado);
        if (req.ProdutoId.HasValue) item.VincularProduto(req.ProdutoId.Value, req.ProdutoDescricao ?? "");
        if (req.FatorConversao.HasValue)
            item.DefinirConversao(req.FatorConversao.Value, req.UnidadeEstoque ?? item.UnidadeEstoque ?? item.UnidadeXml);
        if (req.NumeroLote is not null)
            item.DefinirLote(req.NumeroLote, req.Validade);
        if (req.Tags is not null) item.DefinirTags(req.Tags);

        // Impostos que compõem o custo (IPI / ICMS-ST) — corrigíveis na conferência
        if (req.ValorIpi.HasValue || req.ValorIcmsSt.HasValue)
            item.DefinirImpostos(req.ValorIpi ?? item.ValorIpi, req.ValorIcmsSt ?? item.ValorIcmsSt);

        // Recalcula o custo de TODOS os itens com o frete rateado por VALOR proporcional
        // (a conversão/impostos de um item mudam o custo unitário; o rateio por valor não muda).
        if (req.FatorConversao.HasValue || req.MarkupSugerido.HasValue
            || req.ValorIpi.HasValue || req.ValorIcmsSt.HasValue)
            entrada.RatearFrete();

        if (req.MarkupSugerido.HasValue)
            item.SugerirPreco(req.MarkupSugerido.Value);

        // De-para fornecedor→produto: memoriza o código do produto na nota do emitente,
        // para que próximas entradas do mesmo fornecedor vinculem automaticamente.
        if (req.ProdutoId.HasValue && !string.IsNullOrWhiteSpace(item.CodigoFornecedor))
        {
            var cnpjLimpo = CnpjRaw(entrada.EmitenteCnpj);
            var fornecedor = await db.Fornecedores
                .FirstOrDefaultAsync(f => f.EmpresaId == entrada.EmpresaId && f.Cnpj == cnpjLimpo, ct);
            if (fornecedor is not null)
            {
                var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == req.ProdutoId.Value, ct);
                produto?.VincularReferenciaFornecedor(fornecedor.Id, item.CodigoFornecedor);
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/local-estoque")]
    public async Task<IActionResult> DefinirLocalEstoque(
        Guid id, [FromBody] LocalEstoqueRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.Status == StatusEntradaNFe.Processada)
            return BadRequest(new { mensagem = "Entrada já processada." });

        entrada.DefinirLocalEstoque(req.LocalEstoqueId);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/frete-manual")]
    public async Task<IActionResult> DefinirFreteManual(
        Guid id, [FromBody] FreteManualRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.Status == StatusEntradaNFe.Processada)
            return BadRequest(new { mensagem = "Entrada já processada." });

        entrada.DefinirFreteManual(req.Valor);
        entrada.RatearFrete();
        await db.SaveChangesAsync(ct);
        return Ok(new { freteTotal = entrada.FreteTotal });
    }

    [HttpPatch("{id:guid}/pedido-compra")]
    public async Task<IActionResult> VincularPedidoCompra(
        Guid id, [FromBody] VincularPedidoRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        entrada.VincularPedidoCompra(req.PedidoCompraId);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ──────────────────────────────────────────────────────────────────
    // PROCESSAR (confirmar entrada: movimenta estoque + lança financeiro)
    // ──────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/processar")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> Processar(
        Guid id, [FromBody] ProcessarEntradaRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.Status != StatusEntradaNFe.EmEdicao)
            return BadRequest(new { mensagem = $"Entrada está com status '{entrada.Status}'. Apenas entradas Em Edição podem ser processadas." });

        var itensSemProduto = entrada.Itens.Where(i => i.ProdutoId is null).ToList();
        if (itensSemProduto.Count > 0)
            return BadRequest(new { mensagem = $"{itensSemProduto.Count} item(ns) sem produto vinculado. Vincule todos antes de processar." });

        // Ratear frete nos itens (proporcional ao valor) e calcular o custo final
        entrada.RatearFrete();

        // 1. Movimentar estoque
        foreach (var item in entrada.Itens)
        {
            // Criar lote se necessário
            Guid? loteId = item.LoteId;
            if (loteId is null && item.NumeroLote is not null)
            {
                var lote = Lote.Criar(
                    entrada.EmpresaId, item.ProdutoId!.Value, entrada.LocalEstoqueId,
                    item.NumeroLote, item.QuantidadeEstoque, item.CustoUnitarioFinal,
                    dataValidade: item.Validade);
                db.Lotes.Add(lote);
                await db.SaveChangesAsync(ct);
                loteId = lote.Id;
                item.DefinirLote(item.NumeroLote, item.Validade, loteId);
            }

            var mov = MovimentacaoEstoque.Criar(
                entrada.EmpresaId, item.ProdutoId!.Value, entrada.LocalEstoqueId,
                TipoMovimentacao.Entrada, item.QuantidadeEstoque, item.CustoUnitarioFinal,
                loteId: loteId, documentoOrigem: entrada.ChaveAcesso);
            db.MovimentacoesEstoque.Add(mov);

            // Atualizar estoque atual e custo do produto
            var produto = await db.Produtos.FindAsync([item.ProdutoId], ct);
            if (produto is not null)
            {
                produto.EntradaEstoque(item.QuantidadeEstoque, item.CustoUnitarioFinal);
                if (item.PrecoVendaSugerido.HasValue)
                    produto.AtualizarPrecoEMarkup(item.PrecoVendaSugerido.Value, item.MarkupSugerido ?? produto.Markup);
            }

            item.MarcarEstoqueMovimentado();
        }

        // 2. Lançar faturas em contas a pagar
        var nNF = entrada.ChaveAcesso.Length >= 34
            ? int.Parse(entrada.ChaveAcesso.Substring(25, 9)).ToString()
            : entrada.ChaveAcesso;
        var grupo = Guid.NewGuid().ToString();
        for (int i = 0; i < req.Faturas.Count; i++)
        {
            var f = req.Faturas[i];
            var parcela = i + 1;
            var lanc = LancamentoFinanceiro.Criar(
                entrada.EmpresaId, TipoLancamento.ContaPagar,
                $"{nNF}/{parcela:D3} – {entrada.EmitenteNome}",
                f.Valor, f.Vencimento,
                fornecedorId: entrada.FornecedorId,
                documentoOrigem: entrada.ChaveAcesso,
                parcela: parcela, totalParcelas: req.Faturas.Count,
                grupoParcelamento: grupo);

            // Categoria do contas a pagar + forma de pagamento (observação)
            var obs = string.IsNullOrWhiteSpace(req.FormaPagamento)
                ? null : $"Forma de pagamento: {req.FormaPagamento}";
            lanc.DefinirClassificacao(req.Categoria, null, obs);

            db.LancamentosFinanceiros.Add(lanc);
        }

        entrada.Processar();
        await db.SaveChangesAsync(ct);

        return Ok(new { mensagem = "Entrada processada com sucesso.", id = entrada.Id });
    }

    // ──────────────────────────────────────────────────────────────────
    // ESTORNAR (cancela estoque + cancela financeiro)
    // ──────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/estornar")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Estornar(
        Guid id, [FromBody] EstornarRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        // Estornar movimentações de estoque
        foreach (var item in entrada.Itens.Where(i => i.EstoqueMovimentado && i.ProdutoId.HasValue))
        {
            var mov = MovimentacaoEstoque.Criar(
                entrada.EmpresaId, item.ProdutoId!.Value, entrada.LocalEstoqueId,
                TipoMovimentacao.Saida, item.QuantidadeEstoque, item.CustoUnitarioFinal,
                documentoOrigem: entrada.ChaveAcesso,
                observacao: $"Estorno de entrada: {req.Motivo}");
            db.MovimentacoesEstoque.Add(mov);

            var produto = await db.Produtos.FindAsync([item.ProdutoId], ct);
            produto?.SaidaEstoque(item.QuantidadeEstoque);
        }

        // Cancelar lançamentos financeiros não pagos
        var lancamentos = await db.LancamentosFinanceiros
            .Where(l => l.DocumentoOrigem == entrada.ChaveAcesso &&
                        l.EmpresaId == entrada.EmpresaId &&
                        l.Status == StatusLancamento.EmAberto)
            .ToListAsync(ct);

        foreach (var l in lancamentos)
            l.Cancelar();

        entrada.Estornar(req.Motivo);
        await db.SaveChangesAsync(ct);

        return Ok(new { mensagem = "Entrada estornada.", lancamentosCancelados = lancamentos.Count });
    }

    // ──────────────────────────────────────────────────────────────────
    // CLONAR
    // ──────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/clonar")]
    public async Task<IActionResult> Clonar(Guid id, CancellationToken ct)
    {
        var original = await db.EntradasNFe
            .AsNoTracking().Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        var clone = EntradaNFe.Criar(
            original.EmpresaId, original.NotaFiscalRecebidaId, original.ChaveAcesso,
            original.EmitenteNome, original.EmitenteCnpj, original.DataEmissao,
            original.LocalEstoqueId,
            original.ValorProdutos, original.ValorFrete, original.ValorSeguro,
            original.ValorDesconto, original.ValorIpi, original.ValorIcmsSt, original.ValorTotal,
            original.NaturezaOperacao, original.FornecedorId, original.PedidoCompraId);

        foreach (var item in original.Itens)
        {
            var itemClone = ItemEntradaNFe.Criar(
                clone.Id, item.NumeroItem, item.CfopXml, item.NcmXml, item.DescricaoXml,
                item.QuantidadeXml, item.UnidadeXml, item.ValorUnitarioXml, item.ValorTotalXml,
                item.CodigoFornecedor, item.CodigoBarras, item.ValorIpi, item.ValorIcmsSt);
            if (item.ProdutoId.HasValue)
                itemClone.VincularProduto(item.ProdutoId.Value, item.ProdutoDescricao ?? "");
            if (item.FatorConversao != 1m && item.UnidadeEstoque != null)
                itemClone.DefinirConversao(item.FatorConversao, item.UnidadeEstoque);
            clone.AdicionarItem(itemClone);
        }

        db.EntradasNFe.Add(clone);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obter), new { id = clone.Id }, new { id = clone.Id });
    }

    // ──────────────────────────────────────────────────────────────────
    // EXCLUIR (apenas Em Edição)
    // ──────────────────────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.Status == StatusEntradaNFe.Processada)
            return BadRequest(new { mensagem = "Estorne a entrada antes de excluir." });

        db.EntradasNFe.Remove(entrada);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ──────────────────────────────────────────────────────────────────
    // DEVOLVER (gera NF-e de devolução a partir da entrada)
    // ──────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/devolver")]
    [Authorize(Roles = "Administrador,Financeiro")]
    public async Task<IActionResult> Devolver(
        Guid id, [FromBody] DevolucaoEntradaRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.Status != StatusEntradaNFe.Processada)
            return BadRequest(new { mensagem = "Apenas entradas processadas podem ser devolvidas." });

        // Gerar NF-e de devolução (modelo 55, CFOP 5201/6201)
        var cfop = entrada.EmitenteCnpj.StartsWith(entrada.EmpresaId.ToString()[..2]) ? "5201" : "6201";

        var nfeDev = NotaFiscal.Criar(
            entrada.EmpresaId, ModeloNF.NFe,
            serie: 1, numero: 0, // número será sequenciado pelo sistema
            natureza: NaturezaOperacao.Devolucao);

        nfeDev.DefinirDestinatario(entrada.EmitenteCnpj, entrada.EmitenteNome);

        foreach (var item in entrada.Itens.Where(i => req.Itens == null || req.Itens.Contains(i.Id)))
        {
            if (!item.ProdutoId.HasValue) continue;
            var itemNF = ItemNotaFiscal.Criar(
                nfeDev.Id, item.NumeroItem,
                codigo: item.CodigoFornecedor ?? item.NumeroItem.ToString(),
                descricao: item.DescricaoXml,
                cfop: cfop,
                unidade: item.UnidadeEstoque ?? item.UnidadeXml,
                quantidade: item.QuantidadeEstoque,
                valorUnitario: item.CustoUnitarioFinal,
                ncm: item.NcmXml,
                produtoId: item.ProdutoId);
            nfeDev.AdicionarItem(itemNF);
        }

        db.NotasFiscais.Add(nfeDev);
        await db.SaveChangesAsync(ct);

        return Ok(new { nfeDevolucaoId = nfeDev.Id, mensagem = "NF-e de devolução criada. Acesse Documentos Fiscais para transmitir." });
    }

    // ──────────────────────────────────────────────────────────────────
    // CLONAR PARA SAÍDA (gera NF-e de saída com os itens da entrada)
    // ──────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/clonar-para-saida")]
    public async Task<IActionResult> ClonarParaSaida(Guid id, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe
            .Include(e => e.Itens)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        var nfe = NotaFiscal.Criar(
            entrada.EmpresaId, ModeloNF.NFe,
            serie: 1, numero: 0,
            natureza: NaturezaOperacao.VendaProduto);

        foreach (var item in entrada.Itens.Where(i => i.ProdutoId.HasValue))
        {
            var cfopUtilizado = item.CfopUtilizado;
            var cfopSaida = cfopUtilizado.StartsWith("1") ? "5" + cfopUtilizado[1..]
                          : cfopUtilizado.StartsWith("2") ? "6" + cfopUtilizado[1..]
                          : cfopUtilizado;
            var itemNF = ItemNotaFiscal.Criar(
                nfe.Id, item.NumeroItem,
                codigo: item.CodigoFornecedor ?? item.NumeroItem.ToString(),
                descricao: item.DescricaoXml,
                cfop: cfopSaida,
                unidade: item.UnidadeEstoque ?? item.UnidadeXml,
                quantidade: item.QuantidadeEstoque,
                valorUnitario: item.CustoUnitarioFinal,
                ncm: item.NcmXml,
                produtoId: item.ProdutoId);
            nfe.AdicionarItem(itemNF);
        }

        db.NotasFiscais.Add(nfe);
        await db.SaveChangesAsync(ct);

        return Ok(new { nfeId = nfe.Id, mensagem = "NF-e de saída criada em rascunho. Acesse Documentos Fiscais para editar e transmitir." });
    }

    // ──────────────────────────────────────────────────────────────────
    // IMPRIMIR ETIQUETAS DOS ITENS
    // ──────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/etiquetas")]
    public async Task<IActionResult> GerarEtiquetas(
        Guid id, [FromQuery] Guid templateId, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.AsNoTracking()
            .Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entrada is null) return NotFound();

        // Retorna JSON com itens prontos para impressão (o frontend renderiza as etiquetas)
        var itensEtiqueta = entrada.Itens
            .Where(i => i.ProdutoId.HasValue)
            .Select(i => new
            {
                i.ProdutoId,
                i.ProdutoDescricao,
                i.CodigoBarras,
                i.Validade,
                i.NumeroLote,
                Quantidade = (int)Math.Ceiling(i.QuantidadeEstoque),
                PrecoVenda = i.PrecoVendaSugerido ?? 0m,
                i.Tags,
            });

        return Ok(itensEtiqueta);
    }

    // ──────────────────────────────────────────────────────────────────
    // UTILITÁRIOS PRIVADOS
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parser completo de NF-e XML: extrai emitente, totais, IBS/CBS,
    /// duplicatas e itens (com NCM, CFOP, cProd, cEAN, IPI, ST).
    /// Compatível com nfeProc e NFe como raiz, namespaces NF-e 4.00.
    /// </summary>
    private static NFeParseResult ParsearNFeXml(string xml)
    {
        var result = new NFeParseResult();
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        var ns = doc.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;

        var infNFe = doc.Descendants(ns + "infNFe").FirstOrDefault()
            ?? throw new InvalidOperationException("Elemento <infNFe> não encontrado no XML.");

        result.ChaveAcesso = (infNFe.Attribute("Id")?.Value ?? "").Replace("NFe", "");

        var ide = infNFe.Element(ns + "ide");
        result.Modelo = ide?.Element(ns + "mod")?.Value ?? "55";
        result.Serie = ide?.Element(ns + "serie")?.Value ?? "1";
        result.NumeroNF = long.TryParse(ide?.Element(ns + "nNF")?.Value, out var nNF) ? nNF : 0;
        result.NaturezaOperacao = ide?.Element(ns + "natOp")?.Value;

        var dhEmi = ide?.Element(ns + "dhEmi")?.Value ?? ide?.Element(ns + "dEmi")?.Value;
        result.DataEmissao = DateTime.TryParse(dhEmi, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToUniversalTime() : DateTime.UtcNow;

        var emit = infNFe.Element(ns + "emit");
        result.EmitenteCnpj = emit?.Element(ns + "CNPJ")?.Value ?? emit?.Element(ns + "CPF")?.Value ?? "";
        result.EmitenteNome = emit?.Element(ns + "xNome")?.Value ?? "";
        result.EmitenteNomeFantasia = emit?.Element(ns + "xFant")?.Value;
        result.EmitenteIE = emit?.Element(ns + "IE")?.Value;
        result.EmitenteUF = emit?.Element(ns + "enderEmit")?.Element(ns + "UF")?.Value;
        var enderEmit = emit?.Element(ns + "enderEmit");
        if (enderEmit is not null)
            result.EmitenteEndereco = new EnderecoXml(
                Logradouro: enderEmit.Element(ns + "xLgr")?.Value,
                Numero: enderEmit.Element(ns + "nro")?.Value,
                Complemento: enderEmit.Element(ns + "xCpl")?.Value,
                Bairro: enderEmit.Element(ns + "xBairro")?.Value,
                Municipio: enderEmit.Element(ns + "xMun")?.Value,
                UF: enderEmit.Element(ns + "UF")?.Value,
                Cep: enderEmit.Element(ns + "CEP")?.Value);

        var icmsTot = infNFe.Element(ns + "total")?.Element(ns + "ICMSTot");
        result.ValorProdutos = Dec(icmsTot?.Element(ns + "vProd")?.Value);
        result.ValorFrete    = Dec(icmsTot?.Element(ns + "vFrete")?.Value);
        result.ValorSeguro   = Dec(icmsTot?.Element(ns + "vSeg")?.Value);
        result.ValorDesconto = Dec(icmsTot?.Element(ns + "vDesc")?.Value);
        result.ValorIpi      = Dec(icmsTot?.Element(ns + "vIPI")?.Value);
        result.ValorIcmsSt   = Dec(icmsTot?.Element(ns + "vST")?.Value);
        result.ValorTotal    = Dec(icmsTot?.Element(ns + "vNF")?.Value);

        // IBS/CBS totais — Reforma Tributária EC 132/2023 (campos 2026+)
        var ibscbsTot = infNFe.Element(ns + "total")?.Element(ns + "IBSCBSTot");
        if (ibscbsTot is not null)
        {
            result.ValorIbsTotal = Dec(ibscbsTot.Element(ns + "vIBS")?.Value);
            result.ValorCbsTotal = Dec(ibscbsTot.Element(ns + "vCBS")?.Value);
            if (result.ValorIbsTotal > 0 || result.ValorCbsTotal > 0)
                result.Avisos.Add($"NF-e contém IBS R$ {result.ValorIbsTotal:N2} e CBS R$ {result.ValorCbsTotal:N2} (Reforma Tributária EC 132/2023).");
        }

        // Duplicatas
        foreach (var dup in infNFe.Descendants(ns + "dup"))
        {
            var venc = dup.Element(ns + "dVenc")?.Value;
            result.Duplicatas.Add(new DuplicataXml(
                Numero: dup.Element(ns + "nDup")?.Value ?? "",
                Valor:  Dec(dup.Element(ns + "vDup")?.Value),
                Vencimento: DateTime.TryParse(venc, out var dv) ? dv : DateTime.UtcNow.AddDays(30)));
        }

        // Transporte
        var transp = infNFe.Element(ns + "transp");
        result.ModFrete = transp?.Element(ns + "modFrete")?.Value;
        result.TransportadoraNome = transp?.Element(ns + "transporta")?.Element(ns + "xNome")?.Value;

        // Itens
        int numItem = 0;
        var tempId = Guid.NewGuid();
        foreach (var det in infNFe.Descendants(ns + "det"))
        {
            numItem++;
            var prod = det.Element(ns + "prod");
            var imposto = det.Element(ns + "imposto");
            if (prod is null) continue;

            var ean = prod.Element(ns + "cEAN")?.Value;
            var eanValido = ean is not null && ean.Length >= 8 && ean != "SEM GTIN" ? ean : null;

            var item = ItemEntradaNFe.Criar(
                tempId, numItem,
                cfop:             prod.Element(ns + "CFOP")?.Value ?? "1102",
                ncm:              prod.Element(ns + "NCM")?.Value ?? "",
                descricao:        prod.Element(ns + "xProd")?.Value ?? $"Item {numItem}",
                quantidade:       Dec(prod.Element(ns + "qCom")?.Value),
                unidade:          prod.Element(ns + "uCom")?.Value ?? "UN",
                valorUnitario:    Dec(prod.Element(ns + "vUnCom")?.Value),
                valorTotal:       Dec(prod.Element(ns + "vProd")?.Value),
                codigoFornecedor: prod.Element(ns + "cProd")?.Value,
                codigoBarras:     eanValido,
                valIpi:           Dec(imposto?.Descendants(ns + "vIPI").FirstOrDefault()?.Value),
                valIcmsSt:        Dec(imposto?.Descendants(ns + "vICMSST").FirstOrDefault()?.Value));

            var unidXml = prod.Element(ns + "uCom")?.Value ?? "";
            if (unidXml.Length > 2 && (unidXml.StartsWith("SC") || unidXml.StartsWith("CX") ||
                unidXml.StartsWith("FD") || unidXml.StartsWith("FK") || unidXml.StartsWith("BL")))
                result.Avisos.Add($"Item {numItem} ({item.DescricaoXml[..Math.Min(30, item.DescricaoXml.Length)]}): unidade '{unidXml}' e do fornecedor - defina o fator de conversao.");

            result.Itens.Add(item);
        }

        if (result.ValorTotal == 0 && result.Itens.Count > 0)
            result.ValorTotal = result.Itens.Sum(i => i.ValorTotalXml);

        return result;
    }

    private static List<ItemEntradaNFe> ParsearItensXml(Guid entradaId, string xml)
    {
        try { return ParsearNFeXml(xml).Itens; }
        catch { return []; }
    }

    private static void AtualizarTotaisDoXml(EntradaNFe entrada, string xml)
    {
        // Totais já são carregados via ParsearNFeXml no novo fluxo ImportarXml.
    }

    /// <summary>
    /// Vincula automaticamente os itens da entrada a produtos já cadastrados.
    /// Prioridade: 1) código de barras (EAN); 2) de-para do fornecedor
    /// (CodigoFornecedorPrincipal memorizado em entradas anteriores); 3) código interno.
    /// Retorna quantos itens foram vinculados nesta execução.
    /// </summary>
    private async Task<int> VincularProdutosAutomaticamenteAsync(EntradaNFe entrada, CancellationToken ct)
    {
        var pendentes = entrada.Itens.Where(i => i.ProdutoId is null).ToList();
        if (pendentes.Count == 0) return 0;

        var empresaId = entrada.EmpresaId;
        var codsBarras = pendentes.Where(i => i.CodigoBarras != null).Select(i => i.CodigoBarras!).Distinct().ToList();
        var codsForn = pendentes.Where(i => i.CodigoFornecedor != null).Select(i => i.CodigoFornecedor!).Distinct().ToList();

        var porBarras = codsBarras.Count > 0
            ? await db.Produtos.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId && p.CodigoBarras != null && codsBarras.Contains(p.CodigoBarras))
                .ToDictionaryAsync(p => p.CodigoBarras!, ct)
            : new Dictionary<string, Domain.Estoque.Entities.Produto>();

        // De-para: produtos que já memorizaram o código deste emitente
        var cnpjEmitente = CnpjRaw(entrada.EmitenteCnpj);
        var fornecedor = await db.Fornecedores.AsNoTracking()
            .FirstOrDefaultAsync(f => f.EmpresaId == empresaId && f.Cnpj == cnpjEmitente, ct);

        var porDePara = fornecedor is not null && codsForn.Count > 0
            ? await db.Produtos.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId
                         && p.FornecedorPrincipalId == fornecedor.Id
                         && p.CodigoFornecedorPrincipal != null
                         && codsForn.Contains(p.CodigoFornecedorPrincipal))
                .ToDictionaryAsync(p => p.CodigoFornecedorPrincipal!, ct)
            : new Dictionary<string, Domain.Estoque.Entities.Produto>();

        var porCodigo = codsForn.Count > 0
            ? await db.Produtos.AsNoTracking()
                .Where(p => p.EmpresaId == empresaId && codsForn.Contains(p.Codigo))
                .ToDictionaryAsync(p => p.Codigo, ct)
            : new Dictionary<string, Domain.Estoque.Entities.Produto>();

        int vinculados = 0;
        foreach (var item in pendentes)
        {
            Domain.Estoque.Entities.Produto? prod = null;
            if (item.CodigoBarras != null && porBarras.TryGetValue(item.CodigoBarras, out var pB)) prod = pB;
            else if (item.CodigoFornecedor != null && porDePara.TryGetValue(item.CodigoFornecedor, out var pD)) prod = pD;
            else if (item.CodigoFornecedor != null && porCodigo.TryGetValue(item.CodigoFornecedor, out var pC)) prod = pC;

            if (prod is not null)
            {
                item.VincularProduto(prod.Id, prod.Descricao);
                vinculados++;
            }
        }
        return vinculados;
    }

    /// <summary>Re-executa a vinculação automática dos itens ainda sem produto.</summary>
    [HttpPost("{id:guid}/vincular-automatico")]
    public async Task<IActionResult> VincularAutomatico(Guid id, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.Status == StatusEntradaNFe.Processada)
            return BadRequest(new { mensagem = "Entrada já processada." });

        var vinculados = await VincularProdutosAutomaticamenteAsync(entrada, ct);
        if (vinculados > 0) await db.SaveChangesAsync(ct);

        var pendentes = entrada.Itens.Count(i => i.ProdutoId is null);
        return Ok(new { vinculados, pendentes });
    }

    private static decimal Dec(string? s) =>
        decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;

    // CNPJ alfanumérico (IN RFB 2.229/2024): remove pontuação mas preserva letras A–Z
    private static string CnpjRaw(string cnpj) =>
        cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();
}

// ── Resultado do parse XML ────────────────────────────────────────────
public class NFeParseResult
{
    public string ChaveAcesso { get; set; } = "";
    public string Modelo { get; set; } = "55";
    public string Serie { get; set; } = "1";
    public long NumeroNF { get; set; }
    public string? NaturezaOperacao { get; set; }
    public DateTime DataEmissao { get; set; }
    public string EmitenteCnpj { get; set; } = "";
    public string EmitenteNome { get; set; } = "";
    public string? EmitenteNomeFantasia { get; set; }
    public string? EmitenteIE { get; set; }
    public string? EmitenteUF { get; set; }
    public EnderecoXml? EmitenteEndereco { get; set; }
    public decimal ValorProdutos { get; set; }
    public decimal ValorFrete { get; set; }
    public decimal ValorSeguro { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorIpi { get; set; }
    public decimal ValorIcmsSt { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorIbsTotal { get; set; }
    public decimal ValorCbsTotal { get; set; }
    public string? ModFrete { get; set; }
    public string? TransportadoraNome { get; set; }
    public List<ItemEntradaNFe> Itens { get; set; } = [];
    public List<DuplicataXml> Duplicatas { get; set; } = [];
    public List<string> Avisos { get; set; } = [];
}

public record DuplicataXml(string Numero, decimal Valor, DateTime Vencimento);
public record EnderecoXml(string? Logradouro, string? Numero, string? Complemento,
    string? Bairro, string? Municipio, string? UF, string? Cep);

// ── Requests ──────────────────────────────────────────────────────────
public record IniciarEntradaRequest(Guid EmpresaId, Guid LocalEstoqueId);

public record EditarItemRequest(
    string? CfopUtilizado,
    Guid? ProdutoId,
    string? ProdutoDescricao,
    decimal? FatorConversao,
    string? UnidadeEstoque,
    string? NumeroLote,
    DateTime? Validade,
    string? Tags,
    decimal? MarkupSugerido,
    decimal? ValorIpi = null,
    decimal? ValorIcmsSt = null);

public record LocalEstoqueRequest(Guid LocalEstoqueId);
public record FreteManualRequest(decimal Valor);
public record VincularFornecedorRequest(Guid FornecedorId);
public record VincularPedidoRequest(Guid PedidoCompraId);
public record FaturaRequest(decimal Valor, DateTime Vencimento);
public record ProcessarEntradaRequest(
    List<FaturaRequest> Faturas,
    string? Categoria = null,        // categoria do contas a pagar
    string? FormaPagamento = null);  // forma de pagamento (informativo)
public record EstornarRequest(string Motivo);
public record DevolucaoEntradaRequest(List<Guid>? Itens);
