using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.WhatsApp.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Services;

/// <summary>
/// Atendente virtual de WhatsApp por IA: quando o cliente manda mensagem, responde
/// automaticamente usando o CATÁLOGO real do sistema (produtos + preços da empresa),
/// mantém o contexto da conversa e vai montando o pedido (PedidoWhatsApp) da loja.
/// </summary>
public class WhatsAppIaAtendenteService(
    SistemaDbContext db,
    OpenAiTextService ia,
    WhatsAppCloudApiService whats,
    ILogger<WhatsAppIaAtendenteService> logger)
{
    private record ItemIa(string nome, decimal quantidade);
    private record RespostaIa(string? resposta, List<ItemIa>? itens, bool finalizarPedido);

    /// <summary>Processa UMA mensagem recebida do cliente: chama a IA, envia a resposta,
    /// grava a mensagem enviada e atualiza o pedido. Nunca lança (loga e ignora em erro).</summary>
    public async Task AtenderAsync(ConfiguracaoWhatsAppMensagem cfg, string telefone,
        string? nomeContato, string mensagemCliente, CancellationToken ct)
    {
        try
        {
            if (!ia.Configurado || string.IsNullOrWhiteSpace(cfg.PhoneNumberId) || string.IsNullOrWhiteSpace(cfg.AccessToken))
                return;

            var nomeEmpresa = await db.Empresas.AsNoTracking()
                .Where(e => e.Id == cfg.EmpresaId).Select(e => e.NomeFantasia ?? e.RazaoSocial).FirstOrDefaultAsync(ct)
                ?? "nossa loja";

            // Estoque da LOJA que está atendendo: só oferecer o que TEM saldo nessa loja.
            // Produtos são cadastrados por empresa, mas o estoque é por loja — sem isto a IA
            // ofereceria produto que só existe em outra loja (ex.: coco ralado só em Rio Claro).
            var comSaldo = await ProdutosComSaldoNaLojaAsync(cfg.EmpresaId, cfg.LocalEstoqueId, ct);

            // Catálogo: produtos ativos com preço (e, quando há loja, com saldo nela).
            // PorPeso = vendido por kg (balança/fracionado).
            var catQuery = db.Produtos.AsNoTracking()
                .Where(p => p.EmpresaId == cfg.EmpresaId && p.Ativo && p.PrecoVenda > 0);
            if (comSaldo is not null)
                catQuery = catQuery.Where(p => comSaldo.Contains(p.Id));
            var catalogo = await catQuery
                .OrderBy(p => p.Descricao)
                .Select(p => new { p.Id, p.Descricao, p.PrecoVenda, PorPeso = p.ProdutoBalanca || p.VendidoFracionado })
                .Take(1000).ToListAsync(ct);   // catálogo da loja (cabe no contexto do modelo)
            if (catalogo.Count == 0) return;

            // Histórico recente da conversa (contexto).
            var hist = await db.MensagensWhatsApp.AsNoTracking()
                .Where(m => m.EmpresaId == cfg.EmpresaId && m.Telefone == telefone
                         && (cfg.LocalEstoqueId == null || m.LocalEstoqueId == cfg.LocalEstoqueId))
                .OrderByDescending(m => m.DataHora).Take(10).ToListAsync(ct);
            hist.Reverse();

            var sys = new StringBuilder();
            sys.AppendLine($"Você é o atendente virtual da {nomeEmpresa} (loja de produtos naturais) no WhatsApp.");
            sys.AppendLine("Responda em português, de forma simpática, curta e clara. Use SOMENTE os produtos e preços da lista abaixo — nunca invente produto nem preço.");
            sys.AppendLine("IMPORTANTE: o cliente escreve com ERROS DE DIGITAÇÃO, sem acento ou abreviado. Encontre o produto por SEMELHANÇA na lista (ex.: 'psylium'/'psilio' = PSYLLIUM; 'acafrao'/'curcuma' = CÚRCUMA/AÇAFRÃO; 'linhaça' = LINHACA). Procure BEM na lista inteira antes de dizer que não temos — só diga que não temos se realmente não existir nada parecido.");
            sys.AppendLine("Produtos marcados [por peso] são vendidos por QUILO. SEMPRE informe o preço POR 100g (é assim que o cliente compra); mencione o valor por kg só se ajudar.");
            sys.AppendLine("Ao montar o pedido de item POR PESO, a 'quantidade' deve estar em QUILOS: 100g = 0.1, 250g = 0.25, 500g = 0.5, 1kg = 1. Para itens por unidade, 'quantidade' é o número de unidades.");
            sys.AppendLine("A lista abaixo é o ESTOQUE DESTA LOJA (só o que temos disponível aqui). Ajude o cliente a montar o pedido. Se ele pedir algo que não está na lista, diga que não temos esse produto disponível nesta loja no momento e sugira um similar da lista.");
            sys.AppendLine("Se a conversa sair do escopo (reclamação, troca, entrega complexa, algo que você não sabe), responda que vai chamar um atendente humano.");
            sys.AppendLine("Responda SEMPRE em JSON: {\"resposta\": \"texto que será enviado ao cliente\", \"itens\": [{\"nome\": \"NOME EXATO DA LISTA\", \"quantidade\": N}], \"finalizarPedido\": false}.");
            sys.AppendLine("Em 'itens' liste o pedido ACUMULADO até agora (todos os itens que o cliente quer); use o nome EXATO da lista. Vazio se ainda não pediu nada.");
            sys.AppendLine("'finalizarPedido' = true SOMENTE quando o cliente confirmar que quer fechar o pedido.");

            string Linha(string desc, decimal preco, bool porPeso) => porPeso
                ? $"- {desc} — R$ {preco / 10m:0.00} por 100g (R$ {preco:0.00}/kg) [por peso]"
                : $"- {desc} — R$ {preco:0.00} (unidade)";

            // Busca aproximada: produtos que compartilham trechos (4 letras) com as palavras
            // da mensagem — tolera erro de digitação ("psylium"→"PSYLLIUM") e destaca no topo.
            static string SemAcento(string s) => new string(s.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
            static string Norm(string s) => SemAcento(s.ToLowerInvariant());
            var palavras = Norm(mensagemCliente)
                .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4).Distinct().ToList();
            var gramas = palavras.SelectMany(w => w.Length < 4 ? new[] { w }
                : Enumerable.Range(0, w.Length - 3).Select(i => w.Substring(i, 4))).ToHashSet();
            var relevantes = gramas.Count == 0 ? new List<string>()
                : catalogo.Where(p => { var n = Norm(p.Descricao); return gramas.Any(g => n.Contains(g)); })
                    .Take(30).Select(p => Linha(p.Descricao, p.PrecoVenda, p.PorPeso)).ToList();

            var user = new StringBuilder();
            if (relevantes.Count > 0)
            {
                user.AppendLine("=== PRODUTOS QUE PARECEM COM O QUE O CLIENTE PEDIU (confira AQUI primeiro) ===");
                foreach (var l in relevantes) user.AppendLine(l);
                user.AppendLine();
            }
            user.AppendLine("=== CATÁLOGO COMPLETO (nome — preço) ===");
            foreach (var p in catalogo)
                user.AppendLine(Linha(p.Descricao, p.PrecoVenda, p.PorPeso));
            user.AppendLine();
            user.AppendLine("=== CONVERSA ATÉ AGORA ===");
            foreach (var m in hist)
                user.AppendLine($"{(m.Direcao == DirecaoMensagemWhatsApp.Recebida ? "Cliente" : "Loja")}: {m.Texto}");
            user.AppendLine();
            user.AppendLine($"Nova mensagem do cliente: {mensagemCliente}");

            var json = await ia.GerarChatJsonAsync(sys.ToString(), user.ToString(), ct);
            RespostaIa? r;
            try { r = JsonSerializer.Deserialize<RespostaIa>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { logger.LogWarning("[IA-WHATS] JSON inválido: {Json}", json); return; }

            var texto = r?.resposta?.Trim();
            if (string.IsNullOrWhiteSpace(texto)) return;

            var (ok, wamId, erro) = await whats.EnviarTexto(cfg.PhoneNumberId, cfg.AccessToken, telefone, texto);
            if (!ok) { logger.LogWarning("[IA-WHATS] Falha ao enviar: {Erro}", erro); return; }

            db.MensagensWhatsApp.Add(MensagemWhatsApp.Enviar(
                cfg.EmpresaId, telefone, nomeContato, texto, wamId, localEstoqueId: cfg.LocalEstoqueId));

            if (r!.itens is { Count: > 0 })
                await AtualizarPedidoAsync(cfg, telefone, nomeContato, r.itens, r.finalizarPedido, comSaldo, ct);

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[IA-WHATS] Erro no atendimento por IA (telefone {Tel})", telefone);
        }
    }

    private async Task AtualizarPedidoAsync(ConfiguracaoWhatsAppMensagem cfg, string telefone,
        string? nomeContato, List<ItemIa> itens, bool finalizar, HashSet<Guid>? comSaldo, CancellationToken ct)
    {
        // Produtos da loja (só com saldo, quando informada) para casar pelo nome (exato → contém).
        var prodQuery = db.Produtos.AsNoTracking()
            .Where(p => p.EmpresaId == cfg.EmpresaId && p.Ativo && p.PrecoVenda > 0);
        if (comSaldo is not null)
            prodQuery = prodQuery.Where(p => comSaldo.Contains(p.Id));
        var produtos = await prodQuery
            .Select(p => new { p.Id, p.Descricao, p.PrecoVenda }).ToListAsync(ct);

        // Pedido em aberto (Novo/Confirmado) da loja para este telefone, ou cria.
        var pedido = await db.PedidosWhatsApp
            .Include(p => p.Itens)
            .Where(p => p.EmpresaId == cfg.EmpresaId && p.TelefoneCliente == telefone
                     && (cfg.LocalEstoqueId == null || p.LocalEstoqueId == cfg.LocalEstoqueId)
                     && (p.Status == StatusPedidoWhatsApp.Novo || p.Status == StatusPedidoWhatsApp.Confirmado))
            .OrderByDescending(p => p.CriadoEm).FirstOrDefaultAsync(ct);

        if (pedido is null)
        {
            var numero = $"W{await db.PedidosWhatsApp.CountAsync(p => p.EmpresaId == cfg.EmpresaId, ct) + 1:D4}";
            pedido = PedidoWhatsApp.Criar(cfg.EmpresaId, telefone, nomeContato ?? telefone, numero,
                TipoEntregaWhatsApp.Retirada, localEstoqueId: cfg.LocalEstoqueId);
            db.PedidosWhatsApp.Add(pedido);
        }
        else if (!string.IsNullOrWhiteSpace(nomeContato)) pedido.DefinirNome(nomeContato);

        // Reconstrói os itens a partir do que a IA acumulou.
        pedido.LimparItens();
        foreach (var it in itens)
        {
            if (string.IsNullOrWhiteSpace(it.nome) || it.quantidade <= 0) continue;
            var alvo = it.nome.Trim().ToLowerInvariant();
            var prod = produtos.FirstOrDefault(p => p.Descricao.ToLowerInvariant() == alvo)
                    ?? produtos.FirstOrDefault(p => p.Descricao.ToLowerInvariant().Contains(alvo))
                    ?? produtos.FirstOrDefault(p => alvo.Contains(p.Descricao.ToLowerInvariant()));
            if (prod is null) continue;
            pedido.AdicionarItem(prod.Id, prod.Descricao, it.quantidade, prod.PrecoVenda);
        }

        if (finalizar && pedido.Itens.Count > 0 && pedido.Status == StatusPedidoWhatsApp.Novo)
            pedido.AvancarStatus(StatusPedidoWhatsApp.Confirmado);
    }

    /// <summary>
    /// IDs dos produtos com SALDO &gt; 0 na loja informada. Saldo reconstruído do histórico de
    /// movimentações com o mesmo sinal por tipo do relatório de posição por loja.
    /// Retorna null (não filtra) quando: não há loja definida OU a loja não tem nenhuma
    /// movimentação registrada (estoque não controlado nessa loja — evita "não temos nada").
    /// </summary>
    private async Task<HashSet<Guid>?> ProdutosComSaldoNaLojaAsync(
        Guid empresaId, Guid? localEstoqueId, CancellationToken ct)
    {
        if (localEstoqueId is not Guid loc) return null;

        var ids = await db.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.LocalEstoqueId == loc)
            .GroupBy(m => m.ProdutoId)
            .Select(g => new
            {
                produtoId = g.Key,
                saldo = g.Sum(m =>
                    m.Tipo == TipoMovimentacao.Entrada || m.Tipo == TipoMovimentacao.AjustePositivo
                        || m.Tipo == TipoMovimentacao.Devolucao ? m.Quantidade
                  : m.Tipo == TipoMovimentacao.Saida || m.Tipo == TipoMovimentacao.AjusteNegativo ? -m.Quantidade
                  : m.Tipo == TipoMovimentacao.Transferencia
                        && m.DocumentoOrigem != null && m.DocumentoOrigem.StartsWith("TRANSF<-") ? m.Quantidade
                  : m.Tipo == TipoMovimentacao.Transferencia ? -m.Quantidade
                  : 0m)
            })
            .Where(x => x.saldo > 0)
            .Select(x => x.produtoId)
            .ToListAsync(ct);

        // Loja sem nenhuma movimentação = estoque não controlado ali → não filtra (fallback empresa).
        if (ids.Count == 0)
        {
            var temMov = await db.MovimentacoesEstoque.AsNoTracking()
                .AnyAsync(m => m.EmpresaId == empresaId && m.LocalEstoqueId == loc, ct);
            if (!temMov)
            {
                logger.LogWarning("[IA-WHATS] Loja {Loja} sem movimentações — catálogo sem filtro de saldo.", loc);
                return null;
            }
        }
        return ids.ToHashSet();
    }
}
