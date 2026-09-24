using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.API.Extensions;
using Sistema.Domain.Compras.Entities;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers;

/// <summary>Alertas do sininho (barra superior): etiquetas, estoque, validade, contas.</summary>
[ApiController]
[Route("api/notificacoes")]
[Authorize]
public class NotificacoesController(SistemaDbContext db) : ControllerBase
{
    // Validade pendente só a partir daqui (jul/2026 e anterior = backlog ignorado).
    private static readonly DateTime InicioControleValidade = new(2026, 8, 1);

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
        // Loja do usuário (claim). QUALQUER usuário vinculado a uma loja (atendente, gerente…)
        // vê os alertas por loja SÓ da sua unidade; sem vínculo (ex.: admin) vê de todas.
        var lojaUsuario = User.LojaClaim();

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
            && (lojaUsuario == null || l.LocalEstoqueId == lojaUsuario.Value)
            && l.DataValidade != null && l.DataValidade >= hoje && l.DataValidade <= limiteValidade, ct);

        // Validade/lote pendente de lançamento: nota JÁ recebida (Processada) com item que
        // controla validade e ainda SEM lote. O atendente vê a da SUA loja (precisa lançar);
        // o gestor vê de todas. Cobra o lançamento assim que o produto chega.
        // Início do controle de validade pendente: notas de jul/2026 e anteriores são
        // backlog antigo e ficam de fora (decisão do gestor).
        var corteValidade = hoje.AddDays(-90);
        if (corteValidade < InicioControleValidade) corteValidade = InicioControleValidade;
        var validadePendente = await db.EntradasNFe.CountAsync(e =>
            e.EmpresaId == empresaId && e.Status == StatusEntradaNFe.Processada
            && !e.ValidadePendenteIgnorada
            && e.DataEmissao >= corteValidade
            && (lojaUsuario == null || e.LocalEstoqueId == lojaUsuario.Value)
            && e.Itens.Any(i => i.LoteId == null && i.ProdutoId != null
                && db.Produtos.Any(p => p.Id == i.ProdutoId && p.ControlarValidade)), ct);

        // Entradas de NF-e processadas SEM Ordem de Compra vinculada (esqueceram de vincular na
        // escrituração). Não é atendente; ignora as dispensadas e as notas antigas (jul/2026).
        var entradaSemOc = ehAtendente ? 0 : await db.EntradasNFe.CountAsync(e =>
            e.EmpresaId == empresaId && e.Status == StatusEntradaNFe.Processada
            && !e.VinculoOcIgnorado && e.DataEmissao >= corteValidade
            && (lojaUsuario == null || e.LocalEstoqueId == lojaUsuario.Value)
            && e.PedidoCompraId == null
            && !db.EntradasNFePedidos.Any(v => v.EntradaNFeId == e.Id), ct);

        // Requisições de compra abertas → o gestor precisa gerar os pedidos.
        var ehAdminGerente = User.IsInRole("Administrador") || User.IsInRole("Gerente");
        var requisicoesAbertas = ehAdminGerente ? await db.RequisicoesCompra.CountAsync(r =>
            r.EmpresaId == empresaId && r.Status == StatusRequisicaoCompra.Aberta, ct) : 0;

        // Concorrente mais barato que nós (gestão): produtos em que o menor preço de concorrente ganha.
        var concorrenteMaisBarato = ehAtendente ? 0 : await ContarConcorrenteMaisBaratoAsync(empresaId, ct);

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

        if (validadePendente > 0) itens.Add(new
        {
            tipo = "validade-pendente", quantidade = validadePendente, cor = "deep-orange", icone = "mdi-clipboard-alert-outline",
            titulo = "Validade/lote pendente",
            texto = $"{validadePendente} nota(s) recebida(s) sem validade/lote lançado.",
            rota = "/estoque/validade?pendentes=1"
        });
        if (entradaSemOc > 0) itens.Add(new
        {
            tipo = "entrada-sem-oc", quantidade = entradaSemOc, cor = "brown", icone = "mdi-file-link-outline",
            titulo = "Entrada sem Ordem de Compra",
            texto = $"{entradaSemOc} nota(s) escriturada(s) sem OC vinculada — vincule ou dispense.",
            rota = "/fiscal?aba=entradas&semOc=1"
        });
        if (requisicoesAbertas > 0) itens.Add(new
        {
            tipo = "requisicao", quantidade = requisicoesAbertas, cor = "indigo", icone = "mdi-clipboard-list-outline",
            titulo = "Requisições de compra abertas",
            texto = $"{requisicoesAbertas} requisição(ões) aguardando você gerar o pedido de compra.",
            rota = "/compras/requisicoes"
        });
        if (concorrenteMaisBarato > 0) itens.Add(new
        {
            tipo = "concorrencia", quantidade = concorrenteMaisBarato, cor = "deep-purple", icone = "mdi-tag-arrow-down",
            titulo = "Concorrente mais barato que nós",
            texto = $"{concorrenteMaisBarato} produto(s) com concorrente abaixo do nosso preço.",
            rota = "/concorrencia"
        });

        return Ok(new { total = itens.Count, itens });
    }

    /// <summary>Conta produtos em que o menor preço (última coleta) de um concorrente ativo fica
    /// abaixo do nosso, normalizando por base (granel R$/kg, senão R$/un).</summary>
    private async Task<int> ContarConcorrenteMaisBaratoAsync(Guid empresaId, CancellationToken ct)
    {
        var raw = await db.PrecosConcorrente.AsNoTracking()
            .Join(db.Concorrentes, x => x.ConcorrenteId, c => c.Id, (x, c) => new { x, c })
            .Where(z => z.c.EmpresaId == empresaId && z.c.Ativo && z.x.ProdutoId != null)
            .Select(z => new { z.x.ProdutoId, z.x.ConcorrenteId, z.x.Preco, z.x.Unidade, z.x.DataColeta })
            .ToListAsync(ct);
        if (raw.Count == 0) return 0;

        var ultimos = raw
            .GroupBy(p => new { p.ProdutoId, p.ConcorrenteId })
            .Select(g => g.OrderByDescending(x => x.DataColeta).First())
            .ToList();

        var produtoIds = ultimos.Select(p => p.ProdutoId!.Value).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(x => produtoIds.Contains(x.Id))
            .Select(x => new { x.Id, x.PrecoVenda, porPeso = x.ProdutoBalanca || x.VendidoFracionado })
            .ToDictionaryAsync(x => x.Id, ct);

        var total = 0;
        foreach (var g in ultimos.GroupBy(p => p.ProdutoId!.Value))
        {
            if (!produtos.TryGetValue(g.Key, out var prod) || prod.PrecoVenda <= 0) continue;
            var (_, nosso) = NormalizarPreco(prod.PrecoVenda, prod.porPeso ? "kg" : "un", prod.porPeso);
            var menor = g.Select(x => NormalizarPreco(x.Preco, x.Unidade, prod.porPeso).precoBase).Min();
            if (menor < nosso) total++;
        }
        return total;
    }

    // Normaliza um preço para a base (granel R$/kg, senão R$/un).
    private static (string unidadeBase, decimal precoBase) NormalizarPreco(decimal preco, string? unidade, bool porPeso)
    {
        var u = (unidade ?? "").Trim().ToLowerInvariant();
        if (porPeso)
            return ("kg", u switch { "kg" => preco, "100g" => preco * 10m, "g" => preco * 1000m, _ => preco });
        return ("un", u == "dz" ? Math.Round(preco / 12m, 2) : preco);
    }
}
