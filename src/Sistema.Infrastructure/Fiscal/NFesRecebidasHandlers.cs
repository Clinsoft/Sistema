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

        var ultimoNSU = await db.NotasFiscaisRecebidas
            .Where(n => n.EmpresaId == cmd.EmpresaId)
            .MaxAsync(n => (string?)n.NSU, ct) ?? "0";

        var cnpjLimpo = new string(empresa.Cnpj.Where(char.IsLetterOrDigit).ToArray());
        var resultado = await dfe.ConsultarAsync(cnpjLimpo, empresa.Uf, ultimoNSU, ct);

        if (!resultado.Sucesso)
            return new ResultadoConsulta(false, resultado.Erro, 0, 0);

        var chavesExistentes = await db.NotasFiscaisRecebidas
            .Where(n => n.EmpresaId == cmd.EmpresaId)
            .Select(n => n.ChaveAcesso)
            .ToListAsync(ct);

        var novas = resultado.Documentos
            .Where(d => !chavesExistentes.Contains(d.ChaveAcesso))
            .Select(d => NotaFiscalRecebida.Criar(
                cmd.EmpresaId, d.ChaveAcesso, d.NSU, d.Modelo, d.Serie, d.Numero,
                d.DataEmissao, d.EmitenteCnpj, d.EmitenteNome, d.EmitenteUF,
                d.ValorTotal, d.Situacao))
            .ToList();

        if (novas.Count > 0)
        {
            db.NotasFiscaisRecebidas.AddRange(novas);
            await db.SaveChangesAsync(ct);
        }

        var total = await db.NotasFiscaisRecebidas.CountAsync(n => n.EmpresaId == cmd.EmpresaId, ct);
        return new ResultadoConsulta(true, null, novas.Count, total);
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
