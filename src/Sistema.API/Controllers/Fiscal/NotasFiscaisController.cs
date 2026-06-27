using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/notas")]
[Authorize]
public class NotasFiscaisController(
    INotaFiscalRepository repo,
    IConfiguracaoFiscalRepository configRepo,
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
            n.Status, n.DataEmissao, n.NomeDestinatario, n.TotalNota
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var nota = await repo.ObterComItensAsync(id, ct);
        return nota is null ? NotFound() : Ok(nota);
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
            nota.Status, nota.TotalNota,
            aviso = "Nota em digitação. Use POST /transmitir para enviar à SEFAZ."
        });
    }

    /// <summary>
    /// Stub de transmissão SEFAZ. Fase 2 integrará com NFe.NET/DFe.NET.
    /// </summary>
    [HttpPost("{id:guid}/transmitir")]
    [Authorize(Roles = "Administrador,Financeiro,Contador")]
    public async Task<IActionResult> Transmitir(Guid id, CancellationToken ct)
    {
        var nota = await repo.ObterComItensAsync(id, ct)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != StatusNF.EmDigitacao)
            throw new InvalidOperationException($"Nota com status '{nota.Status}' não pode ser transmitida.");

        // TODO Fase 2: integrar com DFe.NET para gerar e assinar XML, transmitir SEFAZ
        // Por hora, simula autorização em homologação
        var config = await configRepo.ObterPorEmpresaAsync(nota.EmpresaId, ct);
        if (config?.Ambiente == AmbienteFiscal.Producao)
            throw new InvalidOperationException("Integração com SEFAZ (produção) não implementada nesta versão. Use ambiente de homologação.");

        // Simulação homologação
        var chave = $"35{DateTime.Now:yyyyMMdd}000000000000055{nota.Serie:D3}{nota.Numero:D9}1000000000{new Random().Next(10000000, 99999999)}";
        nota.RegistrarTransmissao(chave, "<xml simulado/>");
        nota.RegistrarAutorizacao($"1{DateTime.Now:yyyyMMddHHmmss}000000000", "<xml autorizado simulado/>");

        repo.Atualizar(nota);
        await uow.SalvarAsync(ct);

        return Ok(new
        {
            nota.Id, nota.Numero, nota.ChaveAcesso, nota.Protocolo, nota.Status,
            mensagem = "Nota autorizada (simulação homologação)."
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

        // TODO Fase 2: transmitir evento de cancelamento à SEFAZ
        nota.Cancelar(req.Justificativa);
        repo.Atualizar(nota);
        await uow.SalvarAsync(ct);
        return NoContent();
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
}

public record CriarNFDeVendaRequest(string Modelo = "55");
public record CancelarNFRequest(string Justificativa);
public record InutilizarRequest(
    Guid EmpresaId,
    int Modelo,          // 55 = NF-e, 65 = NFC-e
    int Serie,
    long NumeroInicial,
    long NumeroFinal,
    string Justificativa);
