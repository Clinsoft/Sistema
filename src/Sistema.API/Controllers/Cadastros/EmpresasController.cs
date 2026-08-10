using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

[ApiController]
[Route("api/empresas")]
[Authorize]
public class EmpresasController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var e = await db.Empresas.FindAsync([id], ct);
        if (e is null) return NotFound();
        return Ok(MapearEmpresa(e));
    }

    /// <summary>Lista a matriz e todas as filiais do mesmo grupo.</summary>
    [HttpGet("{id:guid}/grupo")]
    public async Task<IActionResult> ListarGrupo(Guid id, CancellationToken ct)
    {
        var empresa = await db.Empresas.FindAsync([id], ct);
        if (empresa is null) return NotFound();

        // Determina o id da matriz (pode ser a própria empresa ou sua matriz)
        var matrizId = empresa.TipoUnidade == "Filial" ? empresa.MatrizId!.Value : empresa.Id;

        var grupo = await db.Empresas.AsNoTracking()
            .Where(e => e.Ativo && (e.Id == matrizId || e.MatrizId == matrizId))
            .OrderBy(e => e.TipoUnidade).ThenBy(e => e.NomeFantasia)
            .ToListAsync(ct);

        return Ok(grupo.Select(MapearEmpresa));
    }

    /// <summary>Cria uma filial vinculada à empresa matriz informada.</summary>
    [HttpPost("{matrizId:guid}/filiais")]
    public async Task<IActionResult> CriarFilial(Guid matrizId, [FromBody] CriarFilialRequest req, CancellationToken ct)
    {
        var matriz = await db.Empresas.FindAsync([matrizId], ct);
        if (matriz is null) return NotFound("Empresa matriz não encontrada.");

        // Garante que a referência usada é sempre a matriz raiz
        var idMatriz = matriz.TipoUnidade == "Filial" ? matriz.MatrizId!.Value : matriz.Id;

        var filial = Empresa.CriarFilial(
            idMatriz, req.RazaoSocial, req.NomeFantasia, req.Cnpj,
            req.RegimeTributario, req.Logradouro, req.Numero, req.Bairro,
            req.Cidade, req.Uf, req.Cep, req.Telefone, req.Email,
            req.InscricaoEstadual ?? "", req.InscricaoMunicipal ?? "", req.Complemento);

        db.Empresas.Add(filial);
        await uow.SalvarAsync(ct);
        return Ok(MapearEmpresa(filial));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarEmpresaRequest req, CancellationToken ct)
    {
        var e = await db.Empresas.FindAsync([id], ct);
        if (e is null) return NotFound();

        e.Atualizar(req.RazaoSocial, req.NomeFantasia, req.RegimeTributario,
            req.Logradouro, req.Numero, req.Complemento, req.Bairro,
            req.Cidade, req.Uf, req.Cep, req.Telefone, req.Email,
            req.InscricaoEstadual ?? "", req.InscricaoMunicipal ?? "", req.Cnpj);

        db.Empresas.Update(e);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Atualiza os encargos de venda (%) usados na formação de preço / margem líquida.</summary>
    [HttpPut("{id:guid}/encargos-venda")]
    public async Task<IActionResult> AtualizarEncargos(Guid id, [FromBody] EncargosVendaRequest req, CancellationToken ct)
    {
        var e = await db.Empresas.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.DefinirEncargosVenda(req.Imposto, req.Cartao, req.Comissao, req.CustoFixo, req.FaturamentoMedio);
        db.Empresas.Update(e);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Sugere encargos a partir dos cadastros: cartão = maior taxa das operadoras;
    /// custo fixo % = (Despesas Operacionais + Pessoas do mês) ÷ faturamento médio.
    /// </summary>
    [HttpGet("{id:guid}/encargos-sugestao")]
    public async Task<IActionResult> SugerirEncargos(Guid id, CancellationToken ct)
    {
        var e = await db.Empresas.FindAsync([id], ct);
        if (e is null) return NotFound();

        var operadoras = await db.OperadorasCartao.AsNoTracking()
            .Where(o => o.EmpresaId == id)
            .Select(o => new { o.TaxaDebito, o.TaxaCreditoVista, o.TaxaCreditoParcelado, o.TaxaPix, o.TaxaAntecipacao })
            .ToListAsync(ct);
        decimal cartao = 0;
        foreach (var o in operadoras)
            cartao = Math.Max(cartao, Math.Max(o.TaxaDebito,
                Math.Max(o.TaxaCreditoVista, Math.Max(o.TaxaCreditoParcelado, Math.Max(o.TaxaPix, o.TaxaAntecipacao)))));

        var hoje = DateTime.Today;
        string[] cats = ["Despesas Operacionais", "Pessoas"];
        var despesas = await db.LancamentosFinanceiros.AsNoTracking()
            .Where(l => l.EmpresaId == id && l.Tipo == TipoLancamento.ContaPagar
                && l.Categoria != null && cats.Contains(l.Categoria)
                && l.DataVencimento.Month == hoje.Month && l.DataVencimento.Year == hoje.Year)
            .SumAsync(l => (decimal?)l.ValorOriginal, ct) ?? 0m;

        var faturamento = e.FaturamentoMedioMensal;
        var custoFixo = faturamento > 0 ? Math.Round(despesas / faturamento * 100, 2) : 0m;

        return Ok(new { cartao, custoFixo, despesas, faturamentoMedio = faturamento });
    }

    private static object MapearEmpresa(Empresa e) => new
    {
        e.Id, e.RazaoSocial, e.NomeFantasia, e.Cnpj,
        e.InscricaoEstadual, e.InscricaoMunicipal, e.RegimeTributario,
        e.Logradouro, e.Numero, e.Complemento, e.Bairro,
        e.Cidade, e.Uf, e.Cep, e.Telefone, e.Email,
        e.Ativo, e.MatrizId, e.TipoUnidade,
        e.TaxaImpostoVenda, e.TaxaCartao, e.TaxaComissao, e.TaxaCustoFixo,
        e.FaturamentoMedioMensal, e.EncargosVendaTotal
    };
}

public record EncargosVendaRequest(decimal Imposto, decimal Cartao, decimal Comissao,
    decimal CustoFixo, decimal FaturamentoMedio);

public record AtualizarEmpresaRequest(
    string RazaoSocial, string NomeFantasia, string RegimeTributario,
    string Logradouro, string Numero, string? Complemento,
    string Bairro, string Cidade, string Uf, string Cep,
    string Telefone, string Email,
    string? InscricaoEstadual = null, string? InscricaoMunicipal = null,
    string? Cnpj = null);

public record CriarFilialRequest(
    string RazaoSocial, string NomeFantasia, string Cnpj,
    string RegimeTributario, string Logradouro, string Numero, string? Complemento,
    string Bairro, string Cidade, string Uf, string Cep,
    string Telefone, string Email,
    string? InscricaoEstadual = null, string? InscricaoMunicipal = null);
