using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QUnit = QuestPDF.Infrastructure.Unit;
using Sistema.Application.Crediario.Commands;
using Sistema.Domain.Crediario.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Crediario;

[ApiController]
[Route("api/crediario")]
[Authorize]
public class CrediarioController(IMediator mediator, ICrediarioRepository repo, SistemaDbContext db) : ControllerBase
{
    /// <summary>Abre um novo crediário para o cliente.</summary>
    [HttpPost]
    public async Task<IActionResult> Abrir([FromBody] AbrirCrediarioCommand cmd, CancellationToken ct)
    {
        var resultado = await mediator.Send(cmd, ct);
        return Ok(resultado);
    }

    /// <summary>Lista todos os crediários da empresa (para tela de gestão).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId,
        [FromQuery] string? q, CancellationToken ct)
    {
        var crediarios = await db.Crediarios.AsNoTracking()
            .Include(c => c.Parcelas)
            .Where(c => c.EmpresaId == empresaId)
            .Join(db.Clientes, c => c.ClienteId, cl => cl.Id, (c, cl) => new { c, clienteNome = cl.Nome })
            .Where(x => q == null || x.clienteNome.Contains(q))
            .OrderByDescending(x => x.c.DataContrato)
            .ToListAsync(ct);

        return Ok(crediarios.Select(x => new
        {
            x.c.Id, x.c.Numero, x.c.ValorTotal, x.c.ValorFinanciado,
            x.c.NumeroParcelas, x.c.Status, x.c.DataContrato,
            x.clienteNome,
            limiteCredito = 0m,
            saldoDevedor = x.c.SaldoDevedor(),
            inadimplente = x.c.Inadimplente()
        }));
    }

    /// <summary>Retorna as parcelas de um crediário específico.</summary>
    [HttpGet("{id:guid}/parcelas")]
    public async Task<IActionResult> Parcelas(Guid id, CancellationToken ct)
    {
        var crediario = await db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Crediário não encontrado.");

        return Ok(crediario.Parcelas.OrderBy(p => p.Numero).Select(p => new
        {
            p.Id, p.Numero, p.Valor, p.DataVencimento,
            p.DataPagamento, p.ValorPago,
            status = p.Status.ToString(),
            ValorAtualizado = p.ValorAtualizado()
        }));
    }

    /// <summary>Gera o carnê PDF do crediário via QuestPDF.</summary>
    [HttpGet("{id:guid}/carne")]
    public async Task<IActionResult> Carne(Guid id, CancellationToken ct)
    {
        var crediario = await db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Crediário não encontrado.");

        var cliente = await db.Clientes.FindAsync([crediario.ClienteId], ct);

        var pdf = Document.Create(container =>
        {
            foreach (var parcela in crediario.Parcelas.OrderBy(p => p.Numero))
            {
                container.Page(page =>
                {
                    page.Size(14.8f, 10.5f, QUnit.Centimetre);
                    page.Margin(0.8f, QUnit.Centimetre);
                    page.Content().Column(col =>
                    {
                        col.Item().Text($"CARNÊ DE PAGAMENTO — Parcela {parcela.Numero}/{crediario.NumeroParcelas}")
                            .Bold().FontSize(10);
                        col.Item().Text($"Cliente: {cliente?.Nome ?? "-"}").FontSize(9);
                        col.Item().Text($"Vencimento: {parcela.DataVencimento:dd/MM/yyyy}").FontSize(9);
                        col.Item().Text($"Valor: R$ {parcela.Valor:F2}").Bold().FontSize(12);
                        col.Item().PaddingTop(4).Text($"Status: {parcela.Status}").FontSize(8);
                        col.Item().LineHorizontal(0.5f);
                        col.Item().Text("Loja Clinsoft — Produtos Naturais").FontSize(7).Italic();
                    });
                });
            }
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"Carne_{crediario.Numero}.pdf");
    }

    /// <summary>Gera o contrato/cupom de crediário para assinatura do cliente.</summary>
    [HttpGet("{id:guid}/contrato")]
    public async Task<IActionResult> Contrato(Guid id, CancellationToken ct)
    {
        var crediario = await db.Crediarios
            .Include(c => c.Parcelas)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Crediário não encontrado.");

        var cliente = await db.Clientes.FindAsync([crediario.ClienteId], ct);
        var empresa = await db.Empresas.FindAsync([crediario.EmpresaId], ct);

        var nomeEmpresa  = empresa?.NomeFantasia ?? empresa?.RazaoSocial ?? "EcoGranel";
        var cnpjEmpresa  = empresa?.Cnpj ?? "";
        var telEmpresa   = empresa?.Telefone ?? "";
        var nomeCliente  = cliente?.Nome ?? "—";
        var cpfCliente   = cliente?.CpfCnpj ?? "—";
        var telCliente   = cliente?.Telefone ?? "—";

        var totalJuros = crediario.Parcelas.Sum(p => p.Valor) - crediario.ValorFinanciado;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, QUnit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // ── Cabeçalho ──────────────────────────────────────────
                    col.Item().BorderBottom(1).PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(nomeEmpresa).Bold().FontSize(14);
                            c.Item().Text($"CNPJ: {cnpjEmpresa}   Tel: {telEmpresa}").FontSize(8);
                        });
                        row.ConstantItem(160).AlignRight().Column(c =>
                        {
                            c.Item().Text("CONTRATO DE CREDIÁRIO").Bold().FontSize(11).FontColor("#1a3a6b");
                            c.Item().Text($"Nº {crediario.Numero}").FontSize(9);
                            c.Item().Text($"Data: {crediario.DataContrato:dd/MM/yyyy}").FontSize(9);
                        });
                    });

                    col.Item().PaddingTop(10).PaddingBottom(4)
                        .Text("DADOS DO CLIENTE").Bold().FontSize(9).FontColor("#1a3a6b");

                    // ── Dados do cliente ───────────────────────────────────
                    col.Item().Border(0.5f).BorderColor("#cccccc").Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); });
                        t.Cell().Text("Nome:").Bold();
                        t.Cell().Text("CPF/CNPJ:").Bold();
                        t.Cell().Text("Telefone:").Bold();
                        t.Cell().Text(nomeCliente);
                        t.Cell().Text(cpfCliente);
                        t.Cell().Text(telCliente);
                    });

                    col.Item().PaddingTop(10).PaddingBottom(4)
                        .Text("CONDIÇÕES DO CONTRATO").Bold().FontSize(9).FontColor("#1a3a6b");

                    // ── Condições financeiras ──────────────────────────────
                    col.Item().Border(0.5f).BorderColor("#cccccc").Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        t.Cell().Text("Valor Total").Bold();
                        t.Cell().Text("Valor Financiado").Bold();
                        t.Cell().Text("Nº Parcelas").Bold();
                        t.Cell().Text("Taxa Mensal").Bold();
                        t.Cell().Text($"R$ {crediario.ValorTotal:F2}");
                        t.Cell().Text($"R$ {crediario.ValorFinanciado:F2}");
                        t.Cell().Text($"{crediario.NumeroParcelas}x");
                        t.Cell().Text(crediario.TaxaJurosMensal > 0 ? $"{crediario.TaxaJurosMensal:F2}% a.m." : "Sem juros");
                    });

                    col.Item().PaddingTop(10).PaddingBottom(4)
                        .Text("PLANO DE PAGAMENTO").Bold().FontSize(9).FontColor("#1a3a6b");

                    // ── Tabela de parcelas ─────────────────────────────────
                    col.Item().Border(0.5f).BorderColor("#cccccc").Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(40); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                        });

                        // Header
                        foreach (var h in new[] { "Parc.", "Vencimento", "Valor", "Situação" })
                            t.Cell().Background("#1a3a6b").Padding(4)
                                .Text(h).Bold().FontColor("#ffffff").FontSize(8);

                        // Linhas
                        foreach (var p in crediario.Parcelas.OrderBy(p => p.Numero))
                        {
                            var bg = p.Numero % 2 == 0 ? "#f5f7fa" : "#ffffff";
                            t.Cell().Background(bg).Padding(4).Text($"{p.Numero}/{crediario.NumeroParcelas}").FontSize(8);
                            t.Cell().Background(bg).Padding(4).Text(p.DataVencimento.ToString("dd/MM/yyyy")).FontSize(8);
                            t.Cell().Background(bg).Padding(4).Text($"R$ {p.Valor:F2}").FontSize(8);
                            t.Cell().Background(bg).Padding(4).Text("A vencer").FontSize(8);
                        }

                        // Totalizador
                        t.Cell().ColumnSpan(2).Background("#e8edf5").Padding(4)
                            .Text("TOTAL A PAGAR").Bold().FontSize(8);
                        t.Cell().Background("#e8edf5").Padding(4)
                            .Text($"R$ {crediario.Parcelas.Sum(p => p.Valor):F2}").Bold().FontSize(8);
                        t.Cell().Background("#e8edf5").Padding(4)
                            .Text(totalJuros > 0 ? $"Juros: R$ {totalJuros:F2}" : "Sem juros").FontSize(8);
                    });

                    // ── Cláusulas ──────────────────────────────────────────
                    col.Item().PaddingTop(10).PaddingBottom(4)
                        .Text("TERMOS E CONDIÇÕES").Bold().FontSize(9).FontColor("#1a3a6b");

                    col.Item().Border(0.5f).BorderColor("#cccccc").Padding(8).Column(c =>
                    {
                        var clausulas = new[]
                        {
                            "1. O CONTRATANTE compromete-se a efetuar o pagamento de cada parcela até a data de vencimento indicada no Plano de Pagamento.",
                            "2. O atraso no pagamento de qualquer parcela sujeitará o CONTRATANTE a multa de 2% sobre o valor da parcela e juros de mora de 1% ao mês (pro-rata die), além de negativação nos órgãos de proteção ao crédito após 30 dias.",
                            "3. O pagamento poderá ser realizado diretamente no estabelecimento, nas formas aceitas pela loja.",
                            "4. O presente contrato é firmado em caráter irrevogável e irretratável, comprometendo-se as partes ao fiel cumprimento de suas cláusulas.",
                            "5. Fica eleito o foro da comarca do estabelecimento para dirimir quaisquer controvérsias oriundas deste contrato.",
                        };
                        foreach (var cl in clausulas)
                            c.Item().PaddingBottom(3).Text(cl).FontSize(7.5f);
                    });

                    // ── Assinaturas ────────────────────────────────────────
                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().PaddingBottom(30).Text("").FontSize(8); // espaço para assinatura
                            c.Item().BorderTop(0.5f).PaddingTop(4)
                                .Text($"{nomeCliente}").AlignCenter().FontSize(8);
                            c.Item().Text("Contratante — Assinatura e carimbo").AlignCenter().FontSize(7).FontColor("#666666");
                            c.Item().PaddingTop(2).Text($"CPF/CNPJ: {cpfCliente}").AlignCenter().FontSize(7).FontColor("#666666");
                        });
                        row.ConstantItem(30); // espaço central
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().PaddingBottom(30).Text("").FontSize(8);
                            c.Item().BorderTop(0.5f).PaddingTop(4)
                                .Text(nomeEmpresa).AlignCenter().FontSize(8);
                            c.Item().Text("Contratada — Assinatura e carimbo").AlignCenter().FontSize(7).FontColor("#666666");
                        });
                    });

                    // ── Linha de recorte + via do cliente ─────────────────
                    col.Item().PaddingTop(14).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().BorderTop(0.5f).BorderColor("#999999");
                            r.ConstantItem(160).AlignCenter()
                                .Text("✂  recorte aqui").FontSize(7).FontColor("#999999");
                            r.RelativeItem().BorderTop(0.5f).BorderColor("#999999");
                        });
                    });

                    // ── Via do cliente (resumo) ────────────────────────────
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{nomeEmpresa} — VIA DO CLIENTE").Bold().FontSize(8).FontColor("#1a3a6b");
                            c.Item().Text($"Crediário Nº {crediario.Numero}  |  {crediario.DataContrato:dd/MM/yyyy}").FontSize(8);
                            c.Item().Text($"Cliente: {nomeCliente}  |  CPF/CNPJ: {cpfCliente}").FontSize(8);
                            c.Item().Text($"Total: R$ {crediario.Parcelas.Sum(p => p.Valor):F2}  em  {crediario.NumeroParcelas}x de R$ {crediario.Parcelas.FirstOrDefault()?.Valor:F2}").Bold().FontSize(9);
                            c.Item().PaddingTop(4).Text("1ª parcela: " + (crediario.Parcelas.OrderBy(p => p.Numero).FirstOrDefault()?.DataVencimento.ToString("dd/MM/yyyy") ?? "—")).FontSize(8);
                        });
                        row.ConstantItem(180).AlignRight().Column(c =>
                        {
                            c.Item().PaddingBottom(20).Text("").FontSize(8);
                            c.Item().BorderTop(0.5f).PaddingTop(4)
                                .Text("Assinatura do cliente").AlignCenter().FontSize(7).FontColor("#666666");
                        });
                    });
                });
            });
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"Contrato_Crediario_{crediario.Numero}.pdf");
    }

    /// <summary>Lista crediários de um cliente.</summary>
    [HttpGet("cliente/{clienteId:guid}")]
    public async Task<IActionResult> ListarPorCliente(Guid clienteId, [FromQuery] Guid empresaId, CancellationToken ct)
    {
        var crediarios = await repo.ListarPorClienteAsync(empresaId, clienteId, ct);

        return Ok(crediarios.Select(c => new
        {
            c.Id, c.Numero, c.ValorTotal, c.ValorFinanciado,
            c.NumeroParcelas, c.TaxaJurosMensal,
            c.Status, c.DataContrato,
            SaldoDevedor = c.SaldoDevedor(),
            Inadimplente = c.Inadimplente(),
            Parcelas = c.Parcelas.Select(p => new
            {
                p.Id, p.Numero, p.Valor, p.DataVencimento,
                p.DataPagamento, p.ValorPago, p.Status,
                ValorAtualizado = p.ValorAtualizado()
            })
        }));
    }

    /// <summary>Registra o pagamento de uma parcela.</summary>
    [HttpPost("parcelas/{parcelaId:guid}/pagar")]
    public async Task<IActionResult> PagarParcela(Guid parcelaId, [FromBody] PagarParcelaRequest req, CancellationToken ct)
    {
        var resultado = await mediator.Send(new PagarParcelaCommand(parcelaId, req.ValorPago), ct);
        return Ok(resultado);
    }
}

public record PagarParcelaRequest(decimal ValorPago);
