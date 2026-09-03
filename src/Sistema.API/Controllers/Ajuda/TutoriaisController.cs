using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Ajuda.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Ajuda;

[ApiController]
[Route("api/tutoriais")]
[Authorize]
public class TutoriaisController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    /// <summary>Lista os tutoriais. Atendente vê só os ativos.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var q = db.Tutoriais.AsNoTracking().Where(t => t.EmpresaId == empresaId);
        if (User.IsInRole("Atendente")) q = q.Where(t => t.Ativo);
        var lista = await q.OrderBy(t => t.Ordem).ThenBy(t => t.Titulo)
            .Select(t => new { t.Id, t.Titulo, t.Descricao, t.VideoUrl, t.Categoria, t.Ordem, t.Ativo })
            .ToListAsync(ct);
        return Ok(lista);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<IActionResult> Criar([FromBody] TutorialRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Titulo))
            return BadRequest(new { mensagem = "Título é obrigatório." });
        var t = Tutorial.Criar(req.EmpresaId, req.Titulo.Trim(), req.Descricao,
            string.IsNullOrWhiteSpace(req.VideoUrl) ? null : req.VideoUrl.Trim(), req.Categoria, req.Ordem);
        db.Tutoriais.Add(t);
        await uow.SalvarAsync(ct);
        return Ok(new { t.Id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] TutorialRequest req, CancellationToken ct)
    {
        var t = await db.Tutoriais.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Tutorial não encontrado.");
        t.Editar(req.Titulo.Trim(), req.Descricao,
            string.IsNullOrWhiteSpace(req.VideoUrl) ? null : req.VideoUrl.Trim(),
            req.Categoria, req.Ordem, req.Ativo);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        var t = await db.Tutoriais.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Tutorial não encontrado.");
        db.Tutoriais.Remove(t);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Insere os tópicos sugeridos (passo a passo pronto; vídeo fica em branco para o admin preencher).</summary>
    [HttpPost("seed")]
    [Authorize(Roles = "Administrador,Gerente")]
    public async Task<IActionResult> Seed([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var existentes = await db.Tutoriais.AsNoTracking()
            .Where(t => t.EmpresaId == empresaId).Select(t => t.Titulo).ToListAsync(ct);
        var set = existentes.ToHashSet();
        var ordem = existentes.Count;
        var criados = 0;
        foreach (var s in Sugeridos)
        {
            if (set.Contains(s.Titulo)) continue;
            db.Tutoriais.Add(Tutorial.Criar(empresaId, s.Titulo, s.Passos, s.Sim, s.Categoria, ordem++));
            criados++;
        }
        if (criados > 0) await uow.SalvarAsync(ct);
        return Ok(new { criados });
    }

    private static readonly (string Titulo, string Categoria, string Passos, string? Sim)[] Sugeridos =
    [
        ("Abrir o caixa", "PDV",
            "1. Abra o PDV.\n2. Se aparecer \"Nenhum caixa aberto\", escolha o Local de Estoque (sua loja).\n3. Informe o saldo inicial em dinheiro (troco) e confirme.\n4. O caixa fica com a etiqueta verde \"Caixa #\".",
            "/tutoriais/abrir-caixa.html"),
        ("Fazer uma venda", "PDV",
            "1. Bipe o código de barras ou digite o código/nome e pressione Enter.\n2. Para várias unidades, digite 3*código (ex.: 3*789...).\n3. Confira os itens e o total.\n4. Escolha a forma de pagamento (F4 Dinheiro, F6 Pix, F7 Crédito, F8 Débito).\n5. Pressione F10 para finalizar.",
            "/tutoriais/venda.html"),
        ("Buscar e cadastrar cliente", "PDV",
            "1. Aperte F3 (campo Cliente).\n2. Busque por nome, CPF ou telefone.\n3. Aperte Tab para selecionar o primeiro resultado.\n4. Se não existir, clique em \"Cadastrar cliente\" e informe nome, telefone e (opcional) CPF e nascimento.",
            "/tutoriais/cliente.html"),
        ("Venda por peso (balança)", "PDV",
            "1. Bipe a etiqueta da balança (código começa com 2).\n2. O sistema lança o item com o peso/preço da etiqueta.\n3. Para produto por kg sem etiqueta, busque o produto e informe os gramas.",
            "/tutoriais/peso.html"),
        ("Sorteio: gerar o cupom", "PDV",
            "1. A faixa roxa mostra quando a compra dá direito ao cupom.\n2. Finalize a venda (a partir do valor mínimo).\n3. Clique em \"Nova Venda\": abre o cupom.\n4. Preencha nome, telefone e nascimento e gere/imprima o cupom.",
            "/tutoriais/sorteio.html"),
        ("Venda em espera", "PDV",
            "1. Para guardar a venda atual, clique em ⏸ (ou Ctrl+E).\n2. Atenda o próximo cliente.\n3. Para retomar, clique em ▶ (ou Ctrl+L) e escolha a venda.",
            "/tutoriais/espera.html"),
        ("Reimprimir cupom / Sangria", "PDV",
            "Reimprimir: clique na impressora (Ctrl+P) para a 2ª via do último cupom.\nSangria/Reforço: clique no caixa registradora (Ctrl+M), escolha Sangria (retirada) ou Reforço (entrada), informe o valor e confirme.",
            "/tutoriais/caixa-ops.html"),
        ("Requisição de compra", "Compras",
            "1. Menu Requisição de Compra → Nova Requisição.\n2. Busque o produto e informe a quantidade (sem preço, sem fornecedor).\n3. Adicione todos os itens que faltam e envie.\n4. O gestor recebe, agrupa por fornecedor e faz os pedidos.",
            "/tutoriais/requisicao.html"),
    ];
}

public record TutorialRequest(
    Guid EmpresaId, string Titulo, string? Descricao, string? VideoUrl,
    string? Categoria, int Ordem = 0, bool Ativo = true);
