using MediatR;
using Microsoft.EntityFrameworkCore;
using Sistema.Application.Fiscal.Commands;
using Sistema.Domain.Fiscal.Entities;
using Sistema.Domain.Fiscal.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.Infrastructure.Fiscal;

public class ConsultarNFesRecebidasHandler(
    SistemaDbContext db,
    IDistribuicaoDFeService dfe) : IRequestHandler<ConsultarNFesRecebidasCommand, ResultadoConsulta>
{
    public async Task<ResultadoConsulta> Handle(ConsultarNFesRecebidasCommand cmd, CancellationToken ct)
    {
        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == cmd.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        // Carrega config com tracking para poder salvar o NSU depois
        var config = await db.ConfiguracoesFiscais
            .FirstOrDefaultAsync(c => c.EmpresaId == cmd.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Configuração fiscal não encontrada.");

        var cnpjLimpo = new string(empresa.Cnpj.Where(char.IsLetterOrDigit).ToArray());
        var totalNovas = 0;

        // Loop de varredura: repete enquanto SEFAZ retorna documentos (cStat=138).
        // Para quando cStat=137 (sem mais docs) ou em erro.
        var nsuAtual = config.UltimoNsuDFe;
        const int maxIteracoes = 50; // proteção contra loop infinito
        for (int iter = 0; iter < maxIteracoes; iter++)
        {
            var resultado = await dfe.ConsultarAsync(cnpjLimpo, empresa.Uf, nsuAtual, ct);

            if (!resultado.Sucesso)
            {
                if (iter == 0)
                    return new ResultadoConsulta(false, resultado.Erro, 0, 0);
                break; // já processou alguma coisa, para sem propagar erro
            }

            // Salva NSU avançado imediatamente (mesmo se não há docs novas)
            config.AvancarNsuDFe(resultado.UltimoNSU);

            if (resultado.Documentos.Count > 0)
            {
                // Deduplica dentro do próprio lote (mesma chave pode vir como resNFe e
                // em outro documento no mesmo lote), ficando com o maior NSU de cada chave.
                var documentosUnicos = resultado.Documentos
                    .GroupBy(d => d.ChaveAcesso)
                    .Select(g => g.OrderByDescending(d => d.NSU).First())
                    .ToList();

                var chavesExistentes = await db.NotasFiscaisRecebidas
                    .Where(n => n.EmpresaId == cmd.EmpresaId &&
                                documentosUnicos.Select(d => d.ChaveAcesso).Contains(n.ChaveAcesso))
                    .Select(n => n.ChaveAcesso)
                    .ToListAsync(ct);

                var novas = documentosUnicos
                    .Where(d => !chavesExistentes.Contains(d.ChaveAcesso))
                    .Select(d => NotaFiscalRecebida.Criar(
                        cmd.EmpresaId, d.ChaveAcesso, d.NSU, d.Modelo, d.Serie, d.Numero,
                        d.DataEmissao, d.EmitenteCnpj, d.EmitenteNome, d.EmitenteUF,
                        d.ValorTotal, d.Situacao))
                    .ToList();

                if (novas.Count > 0)
                    db.NotasFiscaisRecebidas.AddRange(novas);

                totalNovas += novas.Count;
                await db.SaveChangesAsync(ct); // salva docs e NSU juntos
            }
            else
            {
                await db.SaveChangesAsync(ct); // salva só o NSU atualizado
            }

            // Para quando não há mais docs (cStat=137: TemMais=false)
            if (!resultado.TemMais)
                break;

            nsuAtual = resultado.UltimoNSU;
        }

        // ── Distribuição de CT-e (frete) — webservice e NSU próprios ──
        var nsuCte = config.UltimoNsuCteDFe;
        for (int iter = 0; iter < maxIteracoes; iter++)
        {
            var resultado = await dfe.ConsultarCTeAsync(cnpjLimpo, empresa.Uf, nsuCte, ct);
            if (!resultado.Sucesso) break;

            config.AvancarNsuCteDFe(resultado.UltimoNSU);

            if (resultado.Documentos.Count > 0)
            {
                // Prefere o CT-e COMPLETO (que tem as chaves das NF-e) sobre o resumo.
                var unicos = resultado.Documentos
                    .GroupBy(d => d.ChaveAcesso)
                    .Select(g => g.OrderByDescending(d => d.ChavesReferenciadas != null)
                                  .ThenByDescending(d => d.NSU).First())
                    .ToList();

                var existentes = await db.NotasFiscaisRecebidas
                    .Where(n => n.EmpresaId == cmd.EmpresaId &&
                                unicos.Select(d => d.ChaveAcesso).Contains(n.ChaveAcesso))
                    .ToListAsync(ct);
                var existentesChaves = existentes.Select(n => n.ChaveAcesso).ToHashSet();

                foreach (var d in unicos)
                {
                    // Se já existe mas agora veio o completo com as NF-e, atualiza as chaves.
                    if (existentesChaves.Contains(d.ChaveAcesso))
                    {
                        if (d.ChavesReferenciadas is { Count: > 0 })
                        {
                            var ja = existentes.First(n => n.ChaveAcesso == d.ChaveAcesso);
                            if (string.IsNullOrEmpty(ja.ChavesReferenciadas))
                                ja.DefinirChavesReferenciadas(d.ChavesReferenciadas);
                        }
                        continue;
                    }
                    var nova = NotaFiscalRecebida.Criar(
                        cmd.EmpresaId, d.ChaveAcesso, d.NSU, d.Modelo, d.Serie, d.Numero,
                        d.DataEmissao, d.EmitenteCnpj, d.EmitenteNome, d.EmitenteUF,
                        d.ValorTotal, d.Situacao);
                    if (d.ChavesReferenciadas is { Count: > 0 })
                        nova.DefinirChavesReferenciadas(d.ChavesReferenciadas);
                    db.NotasFiscaisRecebidas.Add(nova);
                    existentesChaves.Add(d.ChaveAcesso);
                    totalNovas++;
                }
            }
            await db.SaveChangesAsync(ct);

            if (!resultado.TemMais) break;
            nsuCte = resultado.UltimoNSU;
        }

        var total = await db.NotasFiscaisRecebidas.CountAsync(n => n.EmpresaId == cmd.EmpresaId, ct);
        return new ResultadoConsulta(true, null, totalNovas, total);
    }
}

public class ManifestarNFeHandler(
    SistemaDbContext db,
    IDistribuicaoDFeService dfe) : IRequestHandler<ManifestarNFeCommand, bool>
{
    public async Task<bool> Handle(ManifestarNFeCommand cmd, CancellationToken ct)
    {
        var nota = await db.NotasFiscaisRecebidas
            .FirstOrDefaultAsync(n => n.Id == cmd.NotaId && n.EmpresaId == cmd.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Nota fiscal não encontrada.");

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == cmd.EmpresaId, ct)!;

        if (cmd.Tipo == ManifestacaoTipo.OperacaoNaoRealizada &&
            string.IsNullOrWhiteSpace(cmd.Justificativa))
            throw new InvalidOperationException("Justificativa obrigatória para 'Operação Não Realizada'.");

        var sucesso = await dfe.ManifestarAsync(
            empresa!.Cnpj, empresa.Uf, nota.ChaveAcesso, cmd.Tipo, cmd.Justificativa, ct);

        nota.Manifestar(cmd.Tipo, cmd.Justificativa);

        if (cmd.Tipo is ManifestacaoTipo.ConfirmacaoOperacao or ManifestacaoTipo.CienciaOperacao)
        {
            var xml = await dfe.BaixarXmlAsync(empresa.Cnpj, empresa.Uf, nota.ChaveAcesso, ct);
            if (xml is not null) nota.SalvarXml(xml);
        }

        await db.SaveChangesAsync(ct);
        return sucesso;
    }
}
