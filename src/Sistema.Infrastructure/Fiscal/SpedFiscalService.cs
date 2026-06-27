using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Estoque.Entities;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Infrastructure.Data;
using System.Text;

namespace Sistema.Infrastructure.Fiscal;

/// <summary>
/// Gera o arquivo SPED Fiscal (EFD ICMS/IPI) — layout 018 vigente.
/// Perfil C (Simples Nacional) e Perfil B (Lucro Presumido).
/// </summary>
public class SpedFiscalService(SistemaDbContext db)
{
    public async Task<byte[]> GerarAsync(Guid empresaId, int ano, int mes, CancellationToken ct)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empresaId, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        var config = await db.ConfiguracoesFiscais.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);

        var notas = await db.NotasFiscais.AsNoTracking()
            .Include(n => n.Itens)
            .Where(n => n.EmpresaId == empresaId
                && n.DataEmissao >= inicio && n.DataEmissao < fim.AddDays(1)
                && n.Status == StatusNF.Autorizada)
            .OrderBy(n => n.DataEmissao)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        var perfil = config?.Regime == RegimeTributario.SimplesNacional ? "C" : "B";

        // ── Bloco 0: Abertura ─────────────────────────────────────────────
        Reg(sb, "0000",
            "015",
            "0",
            inicio.ToString("ddMMyyyy"),
            fim.ToString("ddMMyyyy"),
            empresa.RazaoSocial.ToUpper(),
            LimpaDoc(empresa.Cnpj),
            empresa.InscricaoEstadual ?? "",
            "",
            "",
            empresa.Uf ?? "SP",
            empresa.Uf ?? "SP",
            "",
            perfil,
            "1");

        Reg(sb, "0001", "0");

        var produtoIds = notas.SelectMany(n => n.Itens.Select(i => i.ProdutoId))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => produtoIds.Contains(p.Id))
            .ToListAsync(ct);

        foreach (var p in produtos)
        {
            Reg(sb, "0150",
                p.Id.ToString("N")[..15],
                p.Descricao[..Math.Min(p.Descricao.Length, 60)],
                p.CodigoBarras ?? "",
                p.Codigo,
                p.UnidadeMedidaId.ToString("N")[..6],
                "",
                p.Ncm ?? "00000000",
                p.Cest ?? "",
                "",
                p.CsosnIcms ?? p.CstIcms ?? "400",
                p.CstPisCofins ?? "07",
                p.CstPisCofins ?? "07",
                p.AliquotaIcms.ToString("F2").Replace(",", "."),
                p.AliquotaPis.ToString("F2").Replace(",", "."),
                p.AliquotaCofins.ToString("F2").Replace(",", "."),
                "1");
        }

        Reg(sb, "0990", ContarLinhasBloco(sb, "|0").ToString());

        // ── Bloco C: NF-e Saídas ──────────────────────────────────────────
        Reg(sb, "C001", "0");

        foreach (var nota in notas.Where(n => n.Modelo == ModeloNF.NFe))
        {
            Reg(sb, "C100",
                "1", "1",
                LimpaDoc(nota.CpfCnpjDestinatario ?? ""),
                "55",
                nota.Status == StatusNF.Cancelada ? "02" : "00",
                nota.Serie.ToString(),
                nota.Numero.ToString(),
                nota.ChaveAcesso ?? "",
                nota.DataEmissao.ToString("ddMMyyyy"),
                (nota.DataSaida ?? nota.DataEmissao).ToString("ddMMyyyy"),
                nota.TotalProdutos.ToString("F2").Replace(",", "."),
                "0",
                nota.TotalDesconto.ToString("F2").Replace(",", "."),
                nota.TotalNota.ToString("F2").Replace(",", "."),
                "", "", "",
                nota.TotalIcms.ToString("F2").Replace(",", "."),
                "");

            int numItem = 1;
            foreach (var item in nota.Itens)
            {
                var produto = produtos.FirstOrDefault(p => p.Id == item.ProdutoId);
                Reg(sb, "C170",
                    numItem++.ToString(),
                    produto?.Id.ToString("N")[..15] ?? "",
                    item.Descricao[..Math.Min(item.Descricao.Length, 60)],
                    item.Quantidade.ToString("F4").Replace(",", "."),
                    item.UnidadeMedida,
                    item.ValorUnitario.ToString("F4").Replace(",", "."),
                    item.ValorDesconto.ToString("F2").Replace(",", "."),
                    item.ValorTotal.ToString("F2").Replace(",", "."),
                    item.Cfop,
                    item.CsosnIcms ?? item.CstIcms ?? "400",
                    item.BaseIcms.ToString("F2").Replace(",", "."),
                    item.AliquotaIcms.ToString("F2").Replace(",", "."),
                    item.ValorIcms.ToString("F2").Replace(",", "."),
                    item.CstPisCofins ?? "07",
                    item.ValorPis.ToString("F2").Replace(",", "."),
                    item.CstPisCofins ?? "07",
                    item.ValorCofins.ToString("F2").Replace(",", "."));
            }
        }

        Reg(sb, "C990", (ContarLinhasBloco(sb, "|C") + 2).ToString());

        // ── Bloco H: Inventário ───────────────────────────────────────────
        Reg(sb, "H001", "0");
        Reg(sb, "H005", fim.ToString("ddMMyyyy"), "0", "03");

        foreach (var p in produtos)
        {
            Reg(sb, "H010",
                p.Id.ToString("N")[..15],
                p.UnidadeMedidaId.ToString("N")[..6],
                p.EstoqueAtual.ToString("F3").Replace(",", "."),
                p.CustoUnitario.ToString("F2").Replace(",", "."),
                (p.EstoqueAtual * p.CustoUnitario).ToString("F2").Replace(",", "."),
                "00", "");
        }

        Reg(sb, "H990", (ContarLinhasBloco(sb, "|H") + 2).ToString());

        // ── Bloco 9: Encerramento ─────────────────────────────────────────
        Reg(sb, "9001", "0");

        var linhas = Linhas(sb);
        var grupos = linhas.GroupBy(l => l.Length > 5 ? l[1..5] : "????");
        foreach (var g in grupos.OrderBy(g => g.Key))
            Reg(sb, "9900", g.Key, g.Count().ToString());

        Reg(sb, "9990", (ContarLinhasBloco(sb, "|9") + 2).ToString());
        Reg(sb, "9999", (Linhas(sb).Length + 1).ToString());

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static void Reg(StringBuilder sb, string reg, params string[] campos)
    {
        sb.Append('|').Append(reg).Append('|');
        sb.Append(string.Join("|", campos));
        sb.AppendLine("|");
    }

    private static string[] Linhas(StringBuilder sb)
        => sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static int ContarLinhasBloco(StringBuilder sb, string prefixo)
        => Linhas(sb).Count(l => l.StartsWith(prefixo));

    private static string LimpaDoc(string? doc) =>
        new string((doc ?? "").Where(char.IsDigit).ToArray());
}
