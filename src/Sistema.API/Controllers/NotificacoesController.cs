using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers;

/// <summary>Alertas do sininho (barra superior): etiquetas, estoque, validade, contas.</summary>
[ApiController]
[Route("api/notificacoes")]
[Authorize]
public class NotificacoesController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var hoje = DateTime.Today;
        var limiteValidade = hoje.AddDays(15);

        var etiquetas = await db.Produtos.CountAsync(p =>
            p.EmpresaId == empresaId && p.Ativo && p.EtiquetaDesatualizada, ct);

        var estoqueBaixo = await db.Produtos.CountAsync(p =>
            p.EmpresaId == empresaId && p.Ativo && p.EstoqueMinimo > 0
            && p.EstoqueAtual <= p.EstoqueMinimo, ct);

        var contasVencidas = await db.LancamentosFinanceiros.CountAsync(l =>
            l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaPagar
            && l.Status == StatusLancamento.EmAberto && l.DataVencimento < hoje, ct);

        var validadeProxima = await db.Lotes.CountAsync(l =>
            l.EmpresaId == empresaId && l.Quantidade > 0
            && l.DataValidade != null && l.DataValidade >= hoje && l.DataValidade <= limiteValidade, ct);

        var itens = new List<object>();
        if (etiquetas > 0) itens.Add(new
        {
            tipo = "etiqueta", quantidade = etiquetas, cor = "warning", icone = "mdi-tag-remove-outline",
            titulo = "Etiquetas desatualizadas",
            texto = $"{etiquetas} produto(s) com preço/validade alterado — reimprima a etiqueta.",
            rota = "/estoque/produtos"
        });
        if (estoqueBaixo > 0) itens.Add(new
        {
            tipo = "estoque", quantidade = estoqueBaixo, cor = "error", icone = "mdi-package-variant-remove",
            titulo = "Estoque abaixo do mínimo",
            texto = $"{estoqueBaixo} produto(s) no ou abaixo do estoque mínimo.",
            rota = "/estoque/posicao"
        });
        if (validadeProxima > 0) itens.Add(new
        {
            tipo = "validade", quantidade = validadeProxima, cor = "orange", icone = "mdi-calendar-alert",
            titulo = "Validade próxima",
            texto = $"{validadeProxima} lote(s) vencendo em até 15 dias.",
            rota = "/estoque/validade"
        });
        if (contasVencidas > 0) itens.Add(new
        {
            tipo = "contas", quantidade = contasVencidas, cor = "red-darken-1", icone = "mdi-cash-clock",
            titulo = "Contas a pagar vencidas",
            texto = $"{contasVencidas} conta(s) a pagar em atraso.",
            rota = "/financeiro/contas-pagar?vencidas=1"
        });

        return Ok(new { total = itens.Count, itens });
    }
}
