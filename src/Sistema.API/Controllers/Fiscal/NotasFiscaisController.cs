using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;
using Sistema.Infrastructure.Fiscal;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/notas")]
[Authorize]
public class NotasFiscaisController(
    INotaFiscalRepository repo,
    IConfiguracaoFiscalRepository configRepo,
    INFeTransmissaoService transmissaoService,
    SistemaDbContext db,
    IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim, CancellationToken ct)
    {
        var notas = await repo.ListarPorPeriodoAsync(empresaId, inicio, fim, ct);
        return Ok(notas.Select(n => new
        {
            n.Id, n.Modelo, n.Serie, n.Numero, n.ChaveAcesso,
            Status = n.Status.ToString(),
            n.DataEmissao, n.NomeDestinatario, n.TotalNota
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var nota = await repo.ObterComItensAsync(id, ct);
        return nota is null ? NotFound() : Ok(nota);
    }

    /// <summary>Retorna o XML assinado da nota (enviado à SEFAZ).</summary>
    [HttpGet("{id:guid}/xml")]
    public async Task<IActionResult> ObterXml(Guid id, CancellationToken ct)
    {
        var nota = await repo.ObterPorIdAsync(id, ct);
        if (nota is null) return NotFound();
        if (nota.XmlEnvio is null) return NotFound(new { mensagem = "XML não disponível para esta nota." });
        return Content(nota.XmlEnvio, "application/xml", System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Retorna o DANFE em PDF.
    /// TODO: implementar geração de DANFE com QuestPDF (Sistema.Infrastructure.Fiscal.DanfeBuilder).
    /// </summary>
    [HttpGet("{id:guid}/danfe")]
    public IActionResult ObterDanfe(Guid id)
    {
        // TODO: implementar geração do DANFE com QuestPDF
        return StatusCode(StatusCodes.Status501NotImplemented,
            new { mensagem = "Geração de DANFE ainda não implementada. Em desenvolvimento." });
    }

    /// <summary>
    /// Cria NF-e em digitação a partir de uma venda existente.
    /// A transmissão SEFAZ é feita separadamente via POST /{id}/transmitir.
    /// </summary>
    [HttpPost("de-venda/{vendaId:guid}")]
    public async Task<IActionResult> CriarDeVenda(Guid vendaId, [FromBody] CriarNFDeVendaRequest req, CancellationToken ct)
    {
        var venda = await db.Vendas.Include(v => v.Itens).Include(v => v.Pagamentos)
            .FirstOrDefaultAsync(v => v.Id == vendaId, ct)
            ?? throw new KeyNotFoundException("Venda não encontrada.");

        if (venda.Status != Domain.Vendas.Entities.StatusVenda.Finalizada)
            throw new InvalidOperationException("Só é possível emitir NF de venda finalizada.");

        var config = await configRepo.ObterPorEmpresaAsync(venda.EmpresaId, ct)
            ?? throw new InvalidOperationException("Configuração fiscal não encontrada para a empresa.");

        var modelo = req.Modelo == "65" ? ModeloNF.NFCe : ModeloNF.NFe;
        var numero = modelo == ModeloNF.NFe
            ? config.AvancarNumeracaoNFe()
            : config.AvancarNumeracaoNFCe();

        var nota = NotaFiscal.Criar(venda.EmpresaId, modelo, config.SerieNFe, numero,
            NaturezaOperacao.VendaConsumidor, venda.ClienteId, vendaId: vendaId);

        // Destinatário do cliente se informado
        if (venda.ClienteId.HasValue)
        {
            var cliente = await db.Clientes.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == venda.ClienteId, ct);
            if (cliente is not null)
                nota.DefinirDestinatario(cliente.CpfCnpj ?? "", cliente.Nome, cliente.Email);
        }

        // Itens — carrega produtos e suas unidades de medida
        var produtoIds = venda.Itens.Select(i => i.ProdutoId).ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => produtoIds.Contains(p.Id))
            .ToListAsync(ct);

        var unidadeIds = produtos.Select(p => p.UnidadeMedidaId).Distinct().ToList();
        var unidades = await db.UnidadesMedida.AsNoTracking()
            .Where(u => unidadeIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        int numItem = 1;
        foreach (var item in venda.Itens)
        {
            var produto = produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
            var cfop = produto?.Cfop ?? (config.Regime == RegimeTributario.SimplesNacional ? "5102" : "5102");

            // Unidade de medida real do produto
            var um = produto is not null && unidades.TryGetValue(produto.UnidadeMedidaId, out var u) ? u : null;
            var siglaMedida = um?.Sigla ?? "UN";
            var pesavel = um?.Pesavel ?? false;

            var itemNF = ItemNotaFiscal.Criar(
                nota.Id, numItem++,
                produto?.Codigo ?? item.ProdutoId.ToString()[..8],
                item.Descricao, cfop, siglaMedida,
                item.Quantidade, item.PrecoUnitario,
                ncm: produto?.Ncm, cest: produto?.Cest, produtoId: item.ProdutoId,
                pesavel: pesavel);

            // Calcular impostos conforme regime
            if (config.Regime == RegimeTributario.SimplesNacional)
                itemNF.CalcularImpostos(null, produto?.CsosnIcms ?? "400", 0,
                    produto?.CstPisCofins ?? "07", 0, 0);
            else
                itemNF.CalcularImpostos(produto?.CstIcms ?? "000", null, produto?.AliquotaIcms ?? 0,
                    produto?.CstPisCofins ?? "01", produto?.AliquotaPis ?? 0.65m,
                    produto?.AliquotaCofins ?? 3m);

            nota.AdicionarItem(itemNF);
        }

        configRepo.Atualizar(config);
        await repo.AdicionarAsync(nota, ct);
        await uow.SalvarAsync(ct);

        return Ok(new
        {
            nota.Id, nota.Numero, nota.Serie, nota.Modelo,
            Status = nota.Status.ToString(), nota.TotalNota,
            aviso = "Nota em digitação. Use POST /transmitir para enviar à SEFAZ."
        });
    }

    /// <summary>
    /// Gera e assina o XML, transmite para a SEFAZ e registra o resultado.
    /// </summary>
    [HttpPost("{id:guid}/transmitir")]
    [Authorize(Roles = "Administrador,Financeiro,Contador")]
    public async Task<IActionResult> Transmitir(Guid id, [FromBody] TransmitirNFeRequest? req, CancellationToken ct)
    {
        var nota = await repo.ObterComItensAsync(id, ct)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != StatusNF.EmDigitacao)
            throw new InvalidOperationException($"Nota com status '{nota.Status}' não pode ser transmitida.");

        var config = await configRepo.ObterPorEmpresaAsync(nota.EmpresaId, ct)
            ?? throw new InvalidOperationException("Configuração fiscal não encontrada.");

        if (config.CertificadoPfxBase64 is null)
            throw new InvalidOperationException("Certificado digital A1 não configurado. Acesse Configurações → Fiscal.");

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == nota.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        // Pagamentos: informados no request ou padrão dinheiro pelo total da nota
        var pagamentos = req?.Pagamentos?.Count > 0
            ? req.Pagamentos.Select(p => (p.TPag, p.VPag)).ToList()
            : new List<(string, decimal)> { ("01", nota.TotalNota) };

        // 1. Gerar e assinar XML
        var (xmlAssinado, chave44) = NFeXmlBuilder.GerarXmlAssinado(
            nota, empresa, config,
            pagamentos,
            req?.InformacoesAdicionais);

        nota.RegistrarTransmissao(chave44, xmlAssinado);

        // 2. Transmitir para SEFAZ
        var resultado = await transmissaoService.TransmitirAsync(xmlAssinado, config, ct);

        if (resultado.Autorizada)
            nota.RegistrarAutorizacao(resultado.Protocolo!, resultado.XmlRetorno ?? xmlAssinado);
        else
            nota.RegistrarRejeicao(resultado.MotivoRejeicao ?? "Rejeitada pela SEFAZ",
                resultado.XmlRetorno ?? "");

        repo.Atualizar(nota);
        await uow.SalvarAsync(ct);

        return Ok(new
        {
            nota.Id, nota.Numero, nota.ChaveAcesso, nota.Protocolo,
            Status = nota.Status.ToString(), nota.MotivoRejeicao,
            autorizada = resultado.Autorizada,
            mensagem = resultado.Autorizada
                ? "Nota autorizada pela SEFAZ."
                : $"Nota rejeitada: {resultado.MotivoRejeicao}"
        });
    }

    /// <summary>Cancelar nota autorizada.</summary>
    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Roles = "Administrador,Financeiro,Contador")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarNFRequest req, CancellationToken ct)
    {
        var nota = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != StatusNF.Autorizada)
            throw new InvalidOperationException("Apenas notas autorizadas podem ser canceladas.");

        var justificativa = (req.Justificativa ?? "").Trim();
        if (justificativa.Length < 15 || justificativa.Length > 255)
            return BadRequest(new { mensagem = "A justificativa do cancelamento deve ter entre 15 e 255 caracteres." });

        if (string.IsNullOrWhiteSpace(nota.ChaveAcesso) || string.IsNullOrWhiteSpace(nota.Protocolo))
            return BadRequest(new { mensagem = "Nota sem chave/protocolo de autorização — não é possível cancelar." });

        var config = await configRepo.ObterPorEmpresaAsync(nota.EmpresaId, ct)
            ?? throw new InvalidOperationException("Configuração fiscal não encontrada.");
        if (config.CertificadoPfxBase64 is null)
            return BadRequest(new { mensagem = "Certificado digital A1 não configurado." });

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == nota.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        var cUF = (empresa.CodMunicipio ?? "").Length >= 2 ? empresa.CodMunicipio![..2] : "35";
        var tpAmb = config.Ambiente == Sistema.Domain.Fiscal.Entities.AmbienteFiscal.Producao ? "1" : "2";
        var cnpj = new string((empresa.Cnpj ?? "").Where(char.IsDigit).ToArray());
        var isNFCe = nota.Modelo == ModeloNF.NFCe;

        // 1. Monta e assina o evento de cancelamento (110111).
        var evento = NFeXmlBuilder.GerarEventoCancelamentoAssinado(
            nota.ChaveAcesso!, cnpj, cUF, tpAmb, nota.Protocolo!, justificativa, config);

        // 2. Transmite à SEFAZ (recepção de eventos).
        var resultado = await transmissaoService.CancelarAsync(evento, config, isNFCe, ct);

        if (!resultado.Autorizada)
            return UnprocessableEntity(new
            {
                mensagem = $"Cancelamento não homologado pela SEFAZ: {resultado.MotivoRejeicao}",
                sefaz = resultado.MotivoRejeicao
            });

        nota.Cancelar(resultado.Protocolo ?? "", justificativa);
        repo.Atualizar(nota);
        await uow.SalvarAsync(ct);

        return Ok(new
        {
            nota.Id, Status = nota.Status.ToString(),
            protocoloCancelamento = resultado.Protocolo,
            mensagem = "NFC-e cancelada na SEFAZ com sucesso."
        });
    }
    /// <summary>
    /// Inutiliza faixa de numeração não utilizada na SEFAZ.
    /// Obrigatório quando números foram pulados e não há documento emitido.
    /// </summary>
    [HttpPost("inutilizar")]
    [Authorize(Roles = "Administrador,Contador")]
    public async Task<IActionResult> Inutilizar(
        [FromBody] InutilizarRequest req, CancellationToken ct)
    {
        // Validações básicas
        if (req.NumeroInicial > req.NumeroFinal)
            return BadRequest(new { mensagem = "Número inicial não pode ser maior que o final." });
        if (req.NumeroFinal - req.NumeroInicial > 999)
            return BadRequest(new { mensagem = "Limite de 1000 números por inutilização." });

        // Verificar se não há notas emitidas na faixa
        var notasNaFaixa = await db.NotasFiscais.AnyAsync(n =>
            n.EmpresaId == req.EmpresaId &&
            n.Modelo == (req.Modelo == 55 ? ModeloNF.NFe : ModeloNF.NFCe) &&
            n.Serie == req.Serie &&
            n.Numero >= req.NumeroInicial &&
            n.Numero <= req.NumeroFinal, ct);

        if (notasNaFaixa)
            return Conflict(new { mensagem = "Existem notas emitidas nesta faixa. Verifique os números." });

        // Registrar inutilização como NF com status Inutilizada
        for (var num = req.NumeroInicial; num <= req.NumeroFinal; num++)
        {
            var nf = NotaFiscal.Criar(
                req.EmpresaId,
                req.Modelo == 55 ? ModeloNF.NFe : ModeloNF.NFCe,
                req.Serie, num,
                NaturezaOperacao.VendaProduto);
            // TODO Fase 2: transmitir evento de inutilização à SEFAZ (NFeInutilizacao)
            nf.Inutilizar(req.Justificativa);
            db.NotasFiscais.Add(nf);
        }

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensagem = $"Faixa {req.NumeroInicial}–{req.NumeroFinal} da série {req.Serie} inutilizada.",
            totalInutilizado = req.NumeroFinal - req.NumeroInicial + 1,
        });
    }

    /// <summary>
    /// Cria NF-e avulsa (não vinculada a venda). Após criada, use POST /{id}/transmitir.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarNFeRequest req, CancellationToken ct)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == req.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        var config = await configRepo.ObterPorEmpresaAsync(req.EmpresaId, ct)
            ?? throw new InvalidOperationException("Configuração fiscal não encontrada para a empresa.");

        var modelo = req.Modelo == 65 ? ModeloNF.NFCe : ModeloNF.NFe;
        var numero = modelo == ModeloNF.NFe
            ? config.AvancarNumeracaoNFe()
            : config.AvancarNumeracaoNFCe();

        string? cpfCnpj = req.Destinatario?.CpfCnpj;
        string? nomeDestinatario = req.Destinatario?.Nome;
        string? emailDestinatario = req.Destinatario?.Email;
        Guid? clienteId = req.ClienteId;

        if (req.ClienteId.HasValue)
        {
            var cliente = await db.Clientes.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == req.ClienteId.Value, ct);
            if (cliente is not null)
            {
                cpfCnpj ??= cliente.CpfCnpj;
                nomeDestinatario ??= cliente.Nome;
                emailDestinatario ??= cliente.Email;
            }
        }

        var nota = NotaFiscal.Criar(req.EmpresaId, modelo,
            modelo == ModeloNF.NFe ? config.SerieNFe : config.SerieNFCe,
            numero, NaturezaOperacao.VendaConsumidor, clienteId);

        if (!string.IsNullOrWhiteSpace(cpfCnpj))
            nota.DefinirDestinatario(cpfCnpj, nomeDestinatario ?? "", emailDestinatario);

        int numItem = 1;
        foreach (var itemReq in req.Itens)
        {
            Domain.Estoque.Entities.Produto? produto = null;
            if (itemReq.ProdutoId.HasValue)
                produto = await db.Produtos.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == itemReq.ProdutoId.Value, ct);

            var um = produto is not null
                ? await db.UnidadesMedida.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == produto.UnidadeMedidaId, ct)
                : null;

            var itemNF = ItemNotaFiscal.Criar(
                nota.Id, numItem++,
                produto?.Codigo ?? itemReq.ProdutoId?.ToString()?[..8] ?? numItem.ToString(),
                itemReq.Descricao,
                itemReq.Cfop ?? produto?.Cfop ?? "5102",
                itemReq.Unidade ?? um?.Sigla ?? "UN",
                itemReq.Quantidade,
                itemReq.ValorUnitario,
                itemReq.ValorDesconto,
                ncm: itemReq.Ncm ?? produto?.Ncm,
                cest: itemReq.Cest ?? produto?.Cest,
                produtoId: itemReq.ProdutoId,
                pesavel: um?.Pesavel ?? false);

            if (config.Regime == RegimeTributario.SimplesNacional)
                itemNF.CalcularImpostos(null, itemReq.CsosnIcms ?? produto?.CsosnIcms ?? "400", 0,
                    itemReq.CstPisCofins ?? produto?.CstPisCofins ?? "07", 0, 0);
            else
                itemNF.CalcularImpostos(
                    itemReq.CstIcms ?? produto?.CstIcms ?? "000", null,
                    itemReq.AliqIcms ?? produto?.AliquotaIcms ?? 0,
                    itemReq.CstPisCofins ?? produto?.CstPisCofins ?? "01",
                    itemReq.AliqPis ?? produto?.AliquotaPis ?? 0.65m,
                    itemReq.AliqCofins ?? produto?.AliquotaCofins ?? 3m);

            nota.AdicionarItem(itemNF);
        }

        configRepo.Atualizar(config);
        await repo.AdicionarAsync(nota, ct);
        await uow.SalvarAsync(ct);

        return CreatedAtAction(nameof(Obter), new { id = nota.Id }, new
        {
            nota.Id, nota.Numero, nota.Serie, nota.Modelo,
            Status = nota.Status.ToString(), nota.TotalNota,
            aviso = "Nota em digitação. Use POST /{id}/transmitir para enviar à SEFAZ."
        });
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record CriarNFDeVendaRequest(string Modelo = "55");
public record CancelarNFRequest(string Justificativa);

public record InutilizarRequest(
    Guid EmpresaId,
    int Modelo,          // 55 = NF-e, 65 = NFC-e
    int Serie,
    long NumeroInicial,
    long NumeroFinal,
    string Justificativa);

public record CriarNFeRequest(
    Guid EmpresaId,
    int Modelo,                          // 55 = NF-e, 65 = NFC-e
    Guid? ClienteId,
    DestinatarioNFeDto? Destinatario,
    List<ItemNFeDto> Itens,
    string? InformacoesAdicionais = null);

public record DestinatarioNFeDto(
    string? CpfCnpj,
    string? Nome,
    string? Email);

public record ItemNFeDto(
    Guid? ProdutoId,
    string Descricao,
    decimal Quantidade,
    decimal ValorUnitario,
    decimal ValorDesconto = 0,
    string? Cfop = null,
    string? Ncm = null,
    string? Cest = null,
    string? Unidade = null,
    string? CstIcms = null,
    string? CsosnIcms = null,
    decimal? AliqIcms = null,
    string? CstPisCofins = null,
    decimal? AliqPis = null,
    decimal? AliqCofins = null);

public record TransmitirNFeRequest(
    List<PagamentoNFeDto>? Pagamentos = null,
    string? InformacoesAdicionais = null);

public record PagamentoNFeDto(
    string TPag,   // 01=dinheiro, 03=crédito, 04=débito, 17=PIX, etc.
    decimal VPag);
