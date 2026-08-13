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
        if (dataFim.HasValue) q = q.Where(e => e.DataEmissao < dataFim.Value.AddDays(1));
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
            TipoEntrada = entrada.TipoEntrada.ToString(),
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
                i.MaterialConsumoId,
                i.AtivoImobilizadoId,
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
        var fornecedor = await VincularOuCriarFornecedorAsync(
            empresaId, parsed.EmitenteCnpj, parsed.EmitenteNome,
            parsed.EmitenteNomeFantasia, parsed.EmitenteEndereco, parsed.EmitenteIE, ct);

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
            // EAN (cEAN) é chave forte: casa direto. Já o de-para (código do fornecedor)
            // só vale se a DESCRIÇÃO também bater — senão, quando o fornecedor reaproveita
            // um código antigo, casaria no produto errado. Descrição diferente = fica pendente.
            if (item.CodigoBarras != null && produtosPorBarras.TryGetValue(item.CodigoBarras, out var pBarras))
                prod = pBarras;
            else if (item.CodigoFornecedor != null && produtosPorDePara.TryGetValue(item.CodigoFornecedor, out var pDePara))
            {
                if (DescricoesCompativeis(item.DescricaoXml, pDePara.Descricao))
                    prod = pDePara;
                else
                    parsed.Avisos.Add($"Item {item.NumeroItem} ({item.DescricaoXml[..Math.Min(30, item.DescricaoXml.Length)]}): o código {item.CodigoFornecedor} do fornecedor aponta para '{pDePara.Descricao[..Math.Min(30, pDePara.Descricao.Length)]}', que é outro produto — deixei PENDENTE para você conferir.");
            }

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

        // A nota fica na empresa que a recebeu (ex.: matriz), mas os PRODUTOS,
        // estoque e financeiro podem ir para OUTRA loja do grupo (ex.: filial),
        // quando DestinoEmpresaId é informado. Sem destino, entra na própria empresa.
        var destino = req.DestinoEmpresaId ?? req.EmpresaId;

        // Verificar se já existe entrada para esta nota
        if (await db.EntradasNFe.AnyAsync(e => e.NotaFiscalRecebidaId == notaId, ct))
            return Conflict(new { mensagem = "Já existe uma escrituração para esta NF-e." });

        // Verificar local de estoque (da loja de DESTINO)
        var local = await db.LocaisEstoque
            .FirstOrDefaultAsync(l => l.Id == req.LocalEstoqueId && l.EmpresaId == destino, ct)
            ?? throw new KeyNotFoundException("Local de estoque não encontrado.");

        // Criar entrada com dados da nota recebida
        var entrada = EntradaNFe.Criar(
            destino, notaId, nota.ChaveAcesso,
            nota.EmitenteNome, nota.EmitenteCnpj, nota.DataEmissao,
            req.LocalEstoqueId,
            valProdutos: nota.ValorTotal, valFrete: 0, valSeguro: 0,
            valDesconto: 0, valIpi: 0, valIcmsSt: 0, valTotal: nota.ValorTotal);

        // Se XML disponível, parsear itens E duplicatas (parcelamento da NF-e)
        NFeParseResult? parsed = null;
        if (nota.XmlNota is not null)
        {
            try
            {
                parsed = ParsearNFeXml(nota.XmlNota);
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
            catch { parsed = null; /* XML inválido → segue sem itens/duplicatas */ }
        }

        // Vincular ou criar o fornecedor pelo CNPJ do emitente. Com XML, usa os
        // dados completos (fantasia/endereço/IE); sem XML, os da nota recebida.
        var fornecedor = await VincularOuCriarFornecedorAsync(
            destino,
            parsed?.EmitenteCnpj ?? nota.EmitenteCnpj,
            parsed?.EmitenteNome ?? nota.EmitenteNome,
            parsed?.EmitenteNomeFantasia, parsed?.EmitenteEndereco, parsed?.EmitenteIE, ct);
        if (fornecedor is not null)
            entrada.VincularFornecedor(fornecedor.Id);

        // Vincula automaticamente aos produtos já cadastrados
        // (código de barras → de-para do fornecedor → código interno)
        await VincularProdutosAutomaticamenteAsync(entrada, ct);

        db.EntradasNFe.Add(entrada);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obter), new { id = entrada.Id }, new { id = entrada.Id });
    }

    /// <summary>
    /// Localiza o fornecedor pelo CNPJ do emitente e, se ainda não existir,
    /// cadastra a partir dos dados do XML. Usado por todos os caminhos de
    /// escrituração (XML avulso e NF-e recebida da SEFAZ), para que a entrada
    /// nunca fique sem fornecedor vinculado.
    /// </summary>
    private async Task<Fornecedor?> VincularOuCriarFornecedorAsync(
        Guid empresaId, string? emitenteCnpj, string emitenteNome,
        string? nomeFantasia, EnderecoXml? endereco, string? inscricaoEstadual,
        CancellationToken ct)
    {
        var cnpj = CnpjRaw(emitenteCnpj ?? string.Empty);
        if (string.IsNullOrWhiteSpace(cnpj)) return null;

        var fornecedor = await db.Fornecedores
            .FirstOrDefaultAsync(f => f.EmpresaId == empresaId && f.Cnpj == cnpj, ct);
        if (fornecedor is not null) return fornecedor;

        fornecedor = Fornecedor.Criar(empresaId, emitenteNome, cnpj, nomeFantasia);

        // Endereço completo do XML (quando disponível)
        if (endereco is { } end)
            fornecedor.Editar(
                razaoSocial: emitenteNome, nomeFantasia: nomeFantasia,
                email: null, telefone: null, contato: null, prazoPagamentoDias: 0,
                logradouro: end.Logradouro, numero: end.Numero, complemento: end.Complemento,
                bairro: end.Bairro, cidade: end.Municipio, uf: end.UF, cep: end.Cep,
                inscricaoEstadual: inscricaoEstadual);

        db.Fornecedores.Add(fornecedor);
        return fornecedor;
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

    /// <summary>
    /// Corrige o PRODUTO de um item numa entrada JÁ PROCESSADA, sem estornar a nota toda:
    /// reverte o estoque do produto errado, re-vincula ao produto certo, re-aplica o estoque
    /// e conserta o de-para (código do fornecedor). Atômico; não mexe nos outros itens.
    /// </summary>
    [HttpPost("{id:guid}/itens/{itemId:guid}/corrigir-produto")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> CorrigirProdutoItem(
        Guid id, Guid itemId, [FromBody] CorrigirProdutoItemRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");
        if (entrada.Status != StatusEntradaNFe.Processada)
            return BadRequest(new { mensagem = "Este ajuste é só para entradas já PROCESSADAS. Em 'Em Edição', use o editar item normal." });

        var item = entrada.Itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Item não encontrado.");
        if (item.ProdutoId is null)
            return BadRequest(new { mensagem = "Item sem produto vinculado." });

        var novo = await db.Produtos.FirstOrDefaultAsync(p => p.Id == req.ProdutoId && p.EmpresaId == entrada.EmpresaId, ct);
        if (novo is null) return NotFound(new { mensagem = "Produto de destino não encontrado." });

        var mudaProduto = item.ProdutoId != novo.Id;
        var mudaFator = req.FatorConversao.HasValue && req.FatorConversao.Value > 0
                        && req.FatorConversao.Value != item.FatorConversao;
        if (!mudaProduto && !mudaFator)
            return Ok(new { mensagem = "Nada a alterar (mesmo produto e mesma quantidade)." });

        var antigoId = item.ProdutoId.Value;

        // 1) Reverte o estoque ATUAL do item (produto/qtd/custo de agora).
        if (item.EstoqueMovimentado)
        {
            db.MovimentacoesEstoque.Add(MovimentacaoEstoque.Criar(
                entrada.EmpresaId, antigoId, entrada.LocalEstoqueId,
                TipoMovimentacao.Saida, item.QuantidadeEstoque, item.CustoUnitarioFinal,
                documentoOrigem: entrada.ChaveAcesso,
                observacao: $"Correção de item ({item.DescricaoXml})"));

            var antigo = await db.Produtos.FindAsync([antigoId], ct);
            antigo?.SaidaEstoque(item.QuantidadeEstoque);
            if (mudaProduto && antigo is not null && !string.IsNullOrWhiteSpace(item.CodigoFornecedor)
                && antigo.CodigoFornecedorPrincipal == item.CodigoFornecedor)
                antigo.LimparReferenciaFornecedor();

            if (item.LoteId is Guid loteAntigo)
            {
                var lote = await db.Lotes.FindAsync([loteAntigo], ct);
                lote?.Baixar(item.QuantidadeEstoque);
            }
        }

        // 2) Troca de produto (se pedido).
        if (mudaProduto) item.VincularProduto(novo.Id, novo.Descricao);

        // 3) Ajusta o fator/quantidade (se pedido) e recalcula o custo unitário.
        if (mudaFator)
        {
            item.DefinirConversao(req.FatorConversao!.Value, item.UnidadeEstoque ?? item.UnidadeXml);
            entrada.RatearFrete();   // recomputa CustoUnitarioFinal (total do item ÷ nova quantidade)
        }

        // Quantidade/custo já com produto e fator novos.
        var qtd = item.QuantidadeEstoque;
        var custo = item.CustoUnitarioFinal;

        // 4) Aplica o estoque no produto de destino (espelha o Processar).
        Guid? loteId = null;
        if (!string.IsNullOrWhiteSpace(item.NumeroLote))
        {
            var novoLote = Lote.Criar(entrada.EmpresaId, novo.Id, entrada.LocalEstoqueId,
                item.NumeroLote!, qtd, custo, dataValidade: item.Validade);
            db.Lotes.Add(novoLote);
            await db.SaveChangesAsync(ct);
            loteId = novoLote.Id;
            item.DefinirLote(item.NumeroLote!, item.Validade, loteId);
        }

        db.MovimentacoesEstoque.Add(MovimentacaoEstoque.Criar(
            entrada.EmpresaId, novo.Id, entrada.LocalEstoqueId,
            TipoMovimentacao.Entrada, qtd, custo, loteId: loteId,
            documentoOrigem: entrada.ChaveAcesso));

        novo.EntradaEstoque(qtd, custo);
        if (item.PrecoVendaSugerido.HasValue && novo.PrecoVenda <= 0)
            novo.AtualizarPrecoEMarkup(item.PrecoVendaSugerido.Value, item.MarkupSugerido ?? novo.Markup);

        // 5) Memoriza o de-para no produto de destino.
        if (mudaProduto && entrada.FornecedorId is Guid fid && !string.IsNullOrWhiteSpace(item.CodigoFornecedor))
            novo.VincularReferenciaFornecedor(fid, item.CodigoFornecedor);

        item.MarcarEstoqueMovimentado();
        await db.SaveChangesAsync(ct);

        return Ok(new { mensagem = $"Item corrigido: '{novo.Descricao}', {qtd:0.##} un a R$ {custo:0.00}/un.",
            quantidade = qtd, custoUnitario = custo, produtoAntigo = antigoId, produtoNovo = novo.Id });
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

    /// <summary>
    /// Define se a nota é de mercadoria para venda ou de material de consumo.
    /// Trocar o tipo limpa os vínculos dos itens, que apontam para cadastros diferentes.
    /// </summary>
    [HttpPatch("{id:guid}/tipo-entrada")]
    public async Task<IActionResult> DefinirTipoEntrada(
        Guid id, [FromBody] TipoEntradaRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (!Enum.TryParse<TipoEntradaNFe>(req.Tipo, true, out var tipo))
            return BadRequest(new { mensagem = "Tipo inválido (Mercadoria ou MaterialConsumo)." });

        var jaEraDoTipo = entrada.TipoEntrada == tipo;
        if (!jaEraDoTipo)
        {
            entrada.DefinirTipoEntrada(tipo);
            foreach (var item in entrada.Itens) item.DesvincularCadastro();
        }

        // O CFOP do XML é o do emitente (venda, ex.: 5102) e não serve para a nossa
        // escrituração. Corrige aqui — inclusive em notas já marcadas — para não
        // depender da tela.
        var cfopsCorrigidos = 0;
        if (tipo is TipoEntradaNFe.MaterialConsumo or TipoEntradaNFe.AtivoImobilizado)
        {
            foreach (var item in entrada.Itens.Where(i => EhCfopDeSaida(i.CfopUtilizado)))
            {
                item.DefinirCfop(CfopDeEntrada(item.CfopUtilizado, tipo));
                cfopsCorrigidos++;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            tipo = tipo.ToString(),
            itensDesvinculados = jaEraDoTipo ? 0 : entrada.Itens.Count,
            cfopsCorrigidos,
        });
    }

    /// <summary>
    /// Última conversão usada para cada produto (fator e unidade de estoque), tirada
    /// da entrada mais recente em que ele apareceu. Serve para pré-preencher a etapa
    /// de conversão: quem já recebeu "caixa com 12" antes não precisa informar de novo.
    /// </summary>
    [HttpGet("conversoes-anteriores")]
    public async Task<IActionResult> ConversoesAnteriores(
        [FromQuery] Guid empresaId, [FromQuery] string? produtoIds, CancellationToken ct = default)
    {
        var ids = (produtoIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct().ToList();
        if (ids.Count == 0) return Ok(Array.Empty<object>());

        var itens = await (
            from i in db.Set<ItemEntradaNFe>().AsNoTracking()
            join e in db.EntradasNFe.AsNoTracking() on i.EntradaNFeId equals e.Id
            where e.EmpresaId == empresaId
               && i.ProdutoId != null && ids.Contains(i.ProdutoId.Value)
               && i.UnidadeEstoque != null
            select new { i.ProdutoId, i.FatorConversao, i.UnidadeEstoque, e.CriadoEm }
        ).ToListAsync(ct);

        var ultimas = itens
            .GroupBy(i => i.ProdutoId!.Value)
            .Select(g =>
            {
                var ultima = g.OrderByDescending(x => x.CriadoEm).First();
                return new
                {
                    produtoId = g.Key,
                    fatorConversao = ultima.FatorConversao,
                    unidadeEstoque = ultima.UnidadeEstoque,
                };
            })
            .ToList();

        return Ok(ultimas);
    }

    /// <summary>Vincula um item a um material de consumo já cadastrado.</summary>
    [HttpPatch("{id:guid}/itens/{itemId:guid}/material")]
    public async Task<IActionResult> VincularMaterial(
        Guid id, Guid itemId, [FromBody] VincularMaterialRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");
        var item = entrada.Itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Item não encontrado.");

        var material = await db.MateriaisConsumo.FindAsync([req.MaterialConsumoId], ct)
            ?? throw new KeyNotFoundException("Material não encontrado.");

        item.VincularMaterial(material.Id, material.Descricao);
        if (req.FatorConversao is > 0)
            item.DefinirConversao(req.FatorConversao.Value, item.UnidadeEstoque ?? item.UnidadeXml);
        entrada.RatearFrete();

        await db.SaveChangesAsync(ct);
        return Ok(new { item.Id, materialConsumoId = material.Id, material.Descricao });
    }

    /// <summary>
    /// Cadastra os materiais que faltam a partir dos itens da nota e já vincula.
    /// Reaproveita o de-para (código do material na nota do fornecedor) e o EAN.
    /// </summary>
    [HttpPost("{id:guid}/materiais/cadastrar-faltantes")]
    public async Task<IActionResult> CadastrarMateriaisFaltantes(
        Guid id, [FromBody] CadastrarMateriaisRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.TipoEntrada != TipoEntradaNFe.MaterialConsumo)
            return BadRequest(new { mensagem = "A entrada não está marcada como Material de Consumo." });

        var criados = 0; var vinculados = 0;
        foreach (var item in entrada.Itens.Where(i => i.MaterialConsumoId is null))
        {
            // Já existe? procura por de-para do fornecedor, EAN ou descrição
            var material = await db.MateriaisConsumo.FirstOrDefaultAsync(m =>
                m.EmpresaId == entrada.EmpresaId &&
                ((entrada.FornecedorId != null && m.FornecedorPrincipalId == entrada.FornecedorId
                    && m.CodigoFornecedor != null && m.CodigoFornecedor == item.CodigoFornecedor)
                 || (item.CodigoBarras != null && m.CodigoBarras == item.CodigoBarras)
                 || m.Descricao == item.DescricaoXml), ct);

            if (material is null)
            {
                var codigo = await ProximoCodigoMaterialAsync(entrada.EmpresaId, ct);
                material = MaterialConsumo.Criar(entrada.EmpresaId, codigo,
                    item.DescricaoXml, req.UnidadeMedidaId, entrada.FornecedorId);
                material.Editar(item.DescricaoXml, req.UnidadeMedidaId, entrada.FornecedorId,
                    0, null, null, item.CodigoBarras, true);
                if (entrada.FornecedorId is { } fid)
                    material.VincularReferenciaFornecedor(fid, item.CodigoFornecedor);
                db.MateriaisConsumo.Add(material);
                await db.SaveChangesAsync(ct);
                criados++;
            }

            item.VincularMaterial(material.Id, material.Descricao);
            vinculados++;
        }

        entrada.RatearFrete();
        await db.SaveChangesAsync(ct);
        return Ok(new { criados, vinculados });
    }

    /// <summary>Vincula um item a um bem do ativo imobilizado já cadastrado.</summary>
    [HttpPatch("{id:guid}/itens/{itemId:guid}/ativo")]
    public async Task<IActionResult> VincularAtivo(
        Guid id, Guid itemId, [FromBody] VincularAtivoRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");
        var item = entrada.Itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Item não encontrado.");

        var ativo = await db.AtivosImobilizados.FindAsync([req.AtivoImobilizadoId], ct)
            ?? throw new KeyNotFoundException("Bem não encontrado.");

        item.VincularAtivo(ativo.Id, ativo.Descricao);
        entrada.RatearFrete();
        await db.SaveChangesAsync(ct);
        return Ok(new { item.Id, ativoImobilizadoId = ativo.Id, ativo.Descricao });
    }

    /// <summary>
    /// Cadastra os bens que faltam a partir dos itens da nota e vincula. O valor
    /// de aquisição de cada bem é o custo do item (já com frete e impostos rateados).
    /// </summary>
    [HttpPost("{id:guid}/ativos/cadastrar-faltantes")]
    public async Task<IActionResult> CadastrarAtivosFaltantes(
        Guid id, [FromBody] CadastrarAtivosRequest req, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.TipoEntrada != TipoEntradaNFe.AtivoImobilizado)
            return BadRequest(new { mensagem = "A entrada não está marcada como Ativo Imobilizado." });

        entrada.RatearFrete();
        Enum.TryParse<CategoriaAtivo>(req.Categoria ?? "Equipamento", true, out var cat);

        var criados = 0;
        foreach (var item in entrada.Itens.Where(i => i.AtivoImobilizadoId is null))
        {
            var codigo = await ProximoCodigoAtivoAsync(entrada.EmpresaId, ct);
            var valor = item.CustoUnitarioFinal * item.QuantidadeEstoque;
            var ativo = AtivoImobilizado.Criar(entrada.EmpresaId, codigo, item.DescricaoXml,
                Math.Round(valor, 2), entrada.DataEntrada, cat, entrada.FornecedorId,
                item.QuantidadeEstoque);
            ativo.Editar(item.DescricaoXml, cat, entrada.FornecedorId, Math.Round(valor, 2),
                entrada.DataEntrada, item.QuantidadeEstoque, req.VidaUtilMeses, 0,
                null, null, null, true);
            ativo.DefinirOrigemNota(entrada.ChaveAcesso);

            db.AtivosImobilizados.Add(ativo);
            await db.SaveChangesAsync(ct);

            item.VincularAtivo(ativo.Id, ativo.Descricao);
            criados++;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { criados, vinculados = criados });
    }

    /// <summary>Códigos dos bens são sequenciais a partir de 7001.</summary>
    private async Task<string> ProximoCodigoAtivoAsync(Guid empresaId, CancellationToken ct)
    {
        var codigos = await db.AtivosImobilizados.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId).Select(a => a.Codigo).ToListAsync(ct);
        var maior = codigos.Select(c => int.TryParse(c, out var n) ? n : 0).DefaultIfEmpty(7000).Max();
        return Math.Max(maior + 1, 7001).ToString();
    }

    /// <summary>CFOP de saída (do emitente): 5xxx dentro do estado, 6xxx interestadual.</summary>
    private static bool EhCfopDeSaida(string? cfop) =>
        !string.IsNullOrWhiteSpace(cfop) && (cfop[0] == '5' || cfop[0] == '6');

    /// <summary>
    /// CFOP de entrada derivado do CFOP do emitente e do tipo da nota.
    /// O 1º dígito diz a origem: 5xxx (mesmo estado) → 1xxx; 6xxx (interestadual) → 2xxx.
    /// Uso e consumo → 556; ativo imobilizado → 551.
    /// </summary>
    private static string CfopDeEntrada(string? cfopEmitente, TipoEntradaNFe tipo)
    {
        var interestadual = !string.IsNullOrWhiteSpace(cfopEmitente) && cfopEmitente[0] == '6';
        var origem = interestadual ? "2" : "1";
        var final = tipo == TipoEntradaNFe.AtivoImobilizado ? "551" : "556";
        return origem + final;
    }

    /// <summary>Códigos de materiais são sequenciais a partir de 9001.</summary>
    private async Task<string> ProximoCodigoMaterialAsync(Guid empresaId, CancellationToken ct)
    {
        var codigos = await db.MateriaisConsumo.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId).Select(m => m.Codigo).ToListAsync(ct);
        var maior = codigos.Select(c => int.TryParse(c, out var n) ? n : 0).DefaultIfEmpty(9000).Max();
        return Math.Max(maior + 1, 9001).ToString();
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

        var ehMaterial = entrada.TipoEntrada == TipoEntradaNFe.MaterialConsumo;
        var ehAtivo = entrada.TipoEntrada == TipoEntradaNFe.AtivoImobilizado;

        var semVinculo = entrada.TipoEntrada switch
        {
            TipoEntradaNFe.MaterialConsumo => entrada.Itens.Count(i => i.MaterialConsumoId is null),
            TipoEntradaNFe.AtivoImobilizado => entrada.Itens.Count(i => i.AtivoImobilizadoId is null),
            _ => entrada.Itens.Count(i => i.ProdutoId is null),
        };
        if (semVinculo > 0)
        {
            var oQue = entrada.TipoEntrada switch
            {
                TipoEntradaNFe.MaterialConsumo => "material",
                TipoEntradaNFe.AtivoImobilizado => "bem",
                _ => "produto",
            };
            return BadRequest(new { mensagem =
                $"{semVinculo} item(ns) sem {oQue} vinculado. Vincule todos antes de processar." });
        }

        // Ativo imobilizado: o bem já foi cadastrado com o valor de aquisição.
        // Não há estoque a movimentar — só o financeiro (contas a pagar).
        if (ehAtivo)
        {
            foreach (var item in entrada.Itens) item.MarcarEstoqueMovimentado();
            await LancarFinanceiroEProcessarAsync(entrada, req, ct);
            return Ok(new { mensagem = "Entrada de ativo imobilizado processada.", itens = entrada.Itens.Count });
        }

        // Ratear frete nos itens (proporcional ao valor) e calcular o custo final
        entrada.RatearFrete();

        // Entrada de material de consumo: alimenta o estoque de materiais (não o de
        // mercadorias) — sem lote, sem preço de venda e sem formação de preço.
        if (ehMaterial)
        {
            foreach (var item in entrada.Itens)
            {
                var material = await db.MateriaisConsumo.FindAsync([item.MaterialConsumoId], ct);
                if (material is null) continue;

                material.EntradaEstoque(item.QuantidadeEstoque, item.CustoUnitarioFinal, entrada.DataEntrada);
                db.MovimentacoesMaterial.Add(MovimentacaoMaterial.Criar(
                    entrada.EmpresaId, material.Id, TipoMovimentacaoMaterial.Entrada,
                    item.QuantidadeEstoque, item.CustoUnitarioFinal,
                    documentoOrigem: entrada.ChaveAcesso,
                    observacao: $"NF-e {entrada.EmitenteNome}"));

                if (entrada.FornecedorId is { } fid)
                    material.VincularReferenciaFornecedor(fid, item.CodigoFornecedor);

                item.MarcarEstoqueMovimentado();
            }
            await LancarFinanceiroEProcessarAsync(entrada, req, ct);
            return Ok(new { mensagem = "Entrada de materiais processada.", itens = entrada.Itens.Count });
        }

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
                // Só define o preço de venda quando o produto ainda NÃO tem preço (novo/sem preço).
                // Não sobrescreve o preço de produto já cadastrado — senão a escrituração de uma
                // entrada (ex.: de Rio Claro) mexeria no preço/balança de produtos vendidos em Ipanema.
                if (item.PrecoVendaSugerido.HasValue && produto.PrecoVenda <= 0)
                    produto.AtualizarPrecoEMarkup(item.PrecoVendaSugerido.Value, item.MarkupSugerido ?? produto.Markup);
            }

            item.MarcarEstoqueMovimentado();
        }

        // 2. Lançar faturas em contas a pagar e concluir
        await LancarFinanceiroEProcessarAsync(entrada, req, ct);

        return Ok(new { mensagem = "Entrada processada com sucesso.", id = entrada.Id });
    }

    /// <summary>
    /// Lança as faturas da nota em contas a pagar e marca a entrada como processada.
    /// Compartilhado pelas entradas de mercadoria e de material de consumo — o
    /// financeiro é igual nos dois casos; só o destino do estoque muda.
    /// </summary>
    private async Task LancarFinanceiroEProcessarAsync(
        EntradaNFe entrada, ProcessarEntradaRequest req, CancellationToken ct)
    {
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

            // Categoria do contas a pagar + forma de pagamento (observação).
            // Compra de mercadoria por NF-e entra como "Custo (CMV)" por padrão.
            var obs = string.IsNullOrWhiteSpace(req.FormaPagamento)
                ? null : $"Forma de pagamento: {req.FormaPagamento}";
            var categoria = string.IsNullOrWhiteSpace(req.Categoria) ? "Custo (CMV)" : req.Categoria;
            lanc.DefinirClassificacao(categoria, null, obs);

            db.LancamentosFinanceiros.Add(lanc);
        }

        entrada.Processar();
        await db.SaveChangesAsync(ct);
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

        // Estornar movimentações de estoque de mercadoria
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

        // Estornar o estoque de materiais de consumo (senão o saldo fica inflado)
        foreach (var item in entrada.Itens.Where(i => i.EstoqueMovimentado && i.MaterialConsumoId.HasValue))
        {
            var material = await db.MateriaisConsumo.FindAsync([item.MaterialConsumoId], ct);
            if (material is null) continue;

            material.SaidaEstoque(item.QuantidadeEstoque);
            db.MovimentacoesMaterial.Add(MovimentacaoMaterial.Criar(
                entrada.EmpresaId, material.Id, TipoMovimentacaoMaterial.AjusteNegativo,
                item.QuantidadeEstoque, item.CustoUnitarioFinal,
                documentoOrigem: entrada.ChaveAcesso,
                observacao: $"Estorno de entrada: {req.Motivo}"));
        }

        // Ativo imobilizado: baixa os bens criados por esta nota
        foreach (var item in entrada.Itens.Where(i => i.AtivoImobilizadoId.HasValue))
        {
            var bem = await db.AtivosImobilizados.FindAsync([item.AtivoImobilizadoId], ct);
            if (bem is null || bem.DataBaixa.HasValue) continue;
            bem.Baixar(DateTime.Today, $"Estorno da entrada: {req.Motivo}");
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

            // Lote/Validade do XML: <rastro> (padrão oficial: nLote/dVal) ou, se não houver,
            // o texto livre do infAdProd ("Lote: X Qtde: N Validade: DD/MM/YYYY"). Só PRÉ-PREENCHE
            // (sem criar lote): o Controle de Validade confere e registra.
            var rastro = prod.Element(ns + "rastro");
            string? nLote = rastro?.Element(ns + "nLote")?.Value;
            DateTime? dVal = DateTime.TryParse(rastro?.Element(ns + "dVal")?.Value, out var dvR) ? dvR : null;
            if (nLote is null || dVal is null)
            {
                var infAd = det.Element(ns + "infAdProd")?.Value;
                if (!string.IsNullOrWhiteSpace(infAd))
                {
                    if (nLote is null)
                    {
                        var mL = System.Text.RegularExpressions.Regex.Match(infAd,
                            @"Lote:\s*(.+?)\s+(?:Qtde|Qtd|Validade|Val)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (mL.Success) nLote = mL.Groups[1].Value.Trim();
                    }
                    if (dVal is null)
                    {
                        var mV = System.Text.RegularExpressions.Regex.Match(infAd,
                            @"Validade:\s*(\d{2}/\d{2}/\d{4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (mV.Success && DateTime.TryParseExact(mV.Groups[1].Value, "dd/MM/yyyy",
                                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dvV))
                            dVal = dvV;
                    }
                }
            }
            if (nLote is not null || dVal is not null)
                item.DefinirLote(nLote ?? "", dVal);   // pré-preenche (LoteId fica null = ainda não registrado)

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
            // 1) EAN: chave forte, casa direto. 2) de-para e 3) código interno: só casam
            // se a DESCRIÇÃO também bater — blindagem contra reuso de código pelo fornecedor
            // (que fazia casar no produto errado). Sem descrição parecida, o item fica pendente.
            if (item.CodigoBarras != null && porBarras.TryGetValue(item.CodigoBarras, out var pB))
                prod = pB;
            else if (item.CodigoFornecedor != null && porDePara.TryGetValue(item.CodigoFornecedor, out var pD)
                     && DescricoesCompativeis(item.DescricaoXml, pD.Descricao))
                prod = pD;
            else if (item.CodigoFornecedor != null && porCodigo.TryGetValue(item.CodigoFornecedor, out var pC)
                     && DescricoesCompativeis(item.DescricaoXml, pC.Descricao))
                prod = pC;

            if (prod is not null)
            {
                item.VincularProduto(prod.Id, prod.Descricao);
                vinculados++;
            }
        }
        return vinculados;
    }

    // Palavras em comum entre a descrição do item da NF e a do produto cadastrado.
    // Serve para NÃO casar por código quando o fornecedor reusa um código antigo:
    // se as descrições não se parecem, o item fica pendente para conferência manual.
    private static readonly char[] _tokSep = " /,.;:-()[]+*\t\r\n".ToCharArray();

    private static HashSet<string> TokensDescricao(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new HashSet<string>();
        var semAcento = new string(s.ToUpperInvariant()
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        return semAcento.Split(_tokSep, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToHashSet();
    }

    private static bool DescricoesCompativeis(string? nfDesc, string? produtoDesc)
    {
        var a = TokensDescricao(nfDesc);
        var b = TokensDescricao(produtoDesc);
        if (a.Count == 0 || b.Count == 0) return false;
        var comuns = a.Count(b.Contains);
        // Ao menos 2 palavras em comum, ou 1 que cubra ~1/3 da menor descrição.
        return comuns >= 2 || (comuns >= 1 && comuns * 1.0 / System.Math.Min(a.Count, b.Count) >= 0.34);
    }

    /// <summary>
    /// Reimporta os itens (e duplicatas) do XML da NF-e para uma entrada que ficou
    /// sem itens — normalmente porque foi escriturada antes do XML completo ser baixado
    /// (antes de manifestar). Só age quando a entrada está vazia e Em Edição.
    /// </summary>
    [HttpPost("{id:guid}/reimportar-itens")]
    public async Task<IActionResult> ReimportarItens(Guid id, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        if (entrada.Status != StatusEntradaNFe.EmEdicao)
            return BadRequest(new { mensagem = "Entrada já processada." });
        if (entrada.Itens.Count > 0)
            return BadRequest(new { mensagem = "A entrada já possui itens." });

        var nota = await db.NotasFiscaisRecebidas
            .FirstOrDefaultAsync(n => n.Id == entrada.NotaFiscalRecebidaId, ct);
        if (nota?.XmlNota is null)
            return BadRequest(new { mensagem = "XML completo indisponível. Manifeste a nota (Ciência ou Confirmação) para liberar o XML e tente novamente." });

        NFeParseResult parsed;
        try { parsed = ParsearNFeXml(nota.XmlNota); }
        catch (Exception ex) { return BadRequest(new { mensagem = $"Erro ao ler o XML: {ex.Message}" }); }

        if (parsed.Itens.Count == 0)
            return BadRequest(new { mensagem = "O XML não contém itens (pode ser apenas o resumo). Manifeste a nota para baixar o XML completo." });

        foreach (var item in parsed.Itens)
            entrada.AdicionarItem(item);

        if (parsed.Duplicatas.Count > 0 && string.IsNullOrEmpty(entrada.DuplicatasJson))
        {
            var opts = new System.Text.Json.JsonSerializerOptions
            { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
            entrada.DefinirDuplicatas(System.Text.Json.JsonSerializer.Serialize(parsed.Duplicatas, opts));
        }

        // Entradas antigas podem ter ficado sem fornecedor (criadas antes do XML
        // chegar, ou quando o emitente ainda não era cadastrado): resolve agora.
        var fornecedorNovo = false;
        if (entrada.FornecedorId is null)
        {
            var forn = await VincularOuCriarFornecedorAsync(
                entrada.EmpresaId, parsed.EmitenteCnpj, parsed.EmitenteNome,
                parsed.EmitenteNomeFantasia, parsed.EmitenteEndereco, parsed.EmitenteIE, ct);
            if (forn is not null)
            {
                fornecedorNovo = db.Entry(forn).State == EntityState.Added;
                entrada.VincularFornecedor(forn.Id);
            }
        }

        await db.SaveChangesAsync(ct);

        var vinculados = await VincularProdutosAutomaticamenteAsync(entrada, ct);
        if (vinculados > 0) await db.SaveChangesAsync(ct);

        return Ok(new { itens = parsed.Itens.Count, vinculados, fornecedorNovo });
    }

    /// <summary>
    /// Re-executa a vinculação automática: fornecedor (pelo CNPJ do emitente,
    /// cadastrando se necessário) e itens ainda sem produto.
    /// </summary>
    [HttpPost("{id:guid}/vincular-automatico")]
    public async Task<IActionResult> VincularAutomatico(Guid id, CancellationToken ct)
    {
        var entrada = await db.EntradasNFe.Include(e => e.Itens)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Entrada não encontrada.");

        var processada = entrada.Status == StatusEntradaNFe.Processada;

        // Entrada sem fornecedor (criada antes do emitente existir no cadastro):
        // resolve pelo CNPJ, usando o XML quando disponível para trazer os dados
        // completos. É correção cadastral — vale também para entradas processadas,
        // pois não altera estoque nem os valores do financeiro.
        string? fornecedorNome = null;
        var fornecedorNovo = false;
        var contasCorrigidas = 0;
        var produtosVinculados = 0;
        // 1) Fornecedor da entrada: cadastra/vincula pelo CNPJ se ainda não houver.
        if (entrada.FornecedorId is null)
        {
            NFeParseResult? parsed = null;
            var nota = await db.NotasFiscaisRecebidas
                .FirstOrDefaultAsync(n => n.Id == entrada.NotaFiscalRecebidaId, ct);
            if (nota?.XmlNota is not null)
                try { parsed = ParsearNFeXml(nota.XmlNota); } catch { parsed = null; }

            var forn = await VincularOuCriarFornecedorAsync(
                entrada.EmpresaId,
                parsed?.EmitenteCnpj ?? entrada.EmitenteCnpj,
                parsed?.EmitenteNome ?? entrada.EmitenteNome,
                parsed?.EmitenteNomeFantasia, parsed?.EmitenteEndereco, parsed?.EmitenteIE, ct);

            if (forn is not null)
            {
                fornecedorNovo = db.Entry(forn).State == EntityState.Added;
                entrada.VincularFornecedor(forn.Id);
                await db.SaveChangesAsync(ct);
            }
        }

        // 2) Com o fornecedor definido, corrige o que ficou órfão. Roda mesmo quando
        //    a entrada já estava vinculada — o contas a pagar e o de-para dos produtos
        //    podem ter sido gerados antes de o fornecedor existir.
        if (entrada.FornecedorId is { } fornecedorId)
        {
            var fornecedor = await db.Fornecedores.FirstAsync(f => f.Id == fornecedorId, ct);
            fornecedorNome = fornecedor.RazaoSocial;

            // Contas a pagar da nota sem fornecedor
            var contas = await db.LancamentosFinanceiros
                .Where(l => l.EmpresaId == entrada.EmpresaId
                         && l.DocumentoOrigem == entrada.ChaveAcesso
                         && l.FornecedorId == null)
                .ToListAsync(ct);
            foreach (var c in contas) c.DefinirFornecedor(fornecedorId);
            contasCorrigidas = contas.Count;

            // Produtos da nota sem fornecedor principal → grava fornecedor + de-para
            var idsProdutos = entrada.Itens.Where(i => i.ProdutoId.HasValue)
                .Select(i => i.ProdutoId!.Value).Distinct().ToList();
            if (idsProdutos.Count > 0)
            {
                var produtos = await db.Produtos
                    .Where(p => idsProdutos.Contains(p.Id) && p.FornecedorPrincipalId == null)
                    .ToListAsync(ct);
                foreach (var p in produtos)
                {
                    var it = entrada.Itens.First(i => i.ProdutoId == p.Id);
                    p.VincularReferenciaFornecedor(fornecedorId, it.CodigoFornecedor);
                }
                produtosVinculados = produtos.Count;
            }

            if (contasCorrigidas > 0 || produtosVinculados > 0)
                await db.SaveChangesAsync(ct);
        }

        // Entrada processada: só a correção cadastral acima — os itens já foram
        // movimentados e não devem ser revinculados.
        if (processada)
            return Ok(new { vinculados = 0, pendentes = 0, fornecedorNome, fornecedorNovo,
                            contasCorrigidas, produtosVinculados });

        var vinculados = await VincularProdutosAutomaticamenteAsync(entrada, ct);
        if (vinculados > 0) await db.SaveChangesAsync(ct);

        var pendentes = entrada.Itens.Count(i => i.ProdutoId is null);
        return Ok(new { vinculados, pendentes, fornecedorNome, fornecedorNovo,
                        contasCorrigidas, produtosVinculados });
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
public record IniciarEntradaRequest(Guid EmpresaId, Guid LocalEstoqueId, Guid? DestinoEmpresaId = null);

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

/// <summary>Tipo da entrada: "Mercadoria" ou "MaterialConsumo".</summary>
public record TipoEntradaRequest(string Tipo);

public record VincularMaterialRequest(Guid MaterialConsumoId, decimal? FatorConversao = null);

/// <summary>Unidade padrão para os materiais criados a partir da nota.</summary>
public record CadastrarMateriaisRequest(Guid UnidadeMedidaId);

public record VincularAtivoRequest(Guid AtivoImobilizadoId);

/// <summary>Categoria e vida útil padrão para os bens criados a partir da nota.</summary>
public record CadastrarAtivosRequest(string? Categoria = "Equipamento", int VidaUtilMeses = 60);
public record VincularPedidoRequest(Guid PedidoCompraId);
public record FaturaRequest(decimal Valor, DateTime Vencimento);
public record ProcessarEntradaRequest(
    List<FaturaRequest> Faturas,
    string? Categoria = null,        // categoria do contas a pagar
    string? FormaPagamento = null);  // forma de pagamento (informativo)
public record EstornarRequest(string Motivo);
public record CorrigirProdutoItemRequest(Guid ProdutoId, decimal? FatorConversao = null);
public record DevolucaoEntradaRequest(List<Guid>? Itens);
