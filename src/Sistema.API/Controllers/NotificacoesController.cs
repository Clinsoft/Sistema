using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.API.Extensions;
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

        // Perfil/loja do usuário logado (do JWT):
        //  • Financeiro (contas vencidas) → só Administrador/Financeiro.
        //  • Etiqueta/estoque (tarefas de gestão, sem separação por loja) → não para atendente.
        //  • Validade → filtrada pela loja do atendente (o lote tem loja).
        var ehAtendente = User.EhAtendente();
        var ehGestao = User.IsInRole("Administrador") || User.IsInRole("Financeiro");
        var lojaAtendente = ehAtendente ? (User.LojaClaim() ?? Guid.Empty) : (Guid?)null;

        var etiquetas = ehAtendente ? 0 : await db.Produtos.CountAsync(p =>
            p.EmpresaId == empresaId && p.Ativo && p.EtiquetaDesatualizada, ct);

        var estoqueBaixo = ehAtendente ? 0 : await db.Produtos.CountAsync(p =>
            p.EmpresaId == empresaId && p.Ativo && p.EstoqueMinimo > 0
            && p.EstoqueAtual <= p.EstoqueMinimo, ct);

        var contasVencidas = ehGestao ? await db.LancamentosFinanceiros.CountAsync(l =>
            l.EmpresaId == empresaId && l.Tipo == TipoLancamento.ContaPagar
            && l.Status == StatusLancamento.EmAberto && l.DataVencimento < hoje, ct) : 0;

        var validadeProxima = await db.Lotes.CountAsync(l =>
            l.EmpresaId == empresaId && l.Quantidade > 0
            && (lojaAtendente == null || l.LocalEstoqueId == lojaAtendente.Value)
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
