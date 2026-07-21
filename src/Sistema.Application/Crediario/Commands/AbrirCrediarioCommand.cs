using FluentValidation;
using MediatR;
using CrediarioEntity = Sistema.Domain.Crediario.Entities.Crediario;
using Sistema.Domain.Crediario.Interfaces;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Financeiro.Entities;
using Sistema.Domain.Financeiro.Interfaces;
using Sistema.Domain.Shared.Interfaces;

namespace Sistema.Application.Crediario.Commands;

public record AbrirCrediarioCommand(
    Guid EmpresaId, Guid ClienteId, Guid UsuarioId,
    decimal ValorTotal, decimal ValorEntrada,
    int NumeroParcelas, decimal TaxaJurosMensal,
    Guid? VendaId = null, DateTime? DataPrimeiraParcela = null,
    int? DiaVencimento = null, string? Observacao = null) : IRequest<AbrirCrediarioResult>;

public record AbrirCrediarioResult(Guid Id, string Numero, int NumeroParcelas, decimal ValorParcela);

public class AbrirCrediarioValidator : AbstractValidator<AbrirCrediarioCommand>
{
    public AbrirCrediarioValidator()
    {
        RuleFor(x => x.ValorTotal).GreaterThan(0);
        RuleFor(x => x.ValorEntrada).GreaterThanOrEqualTo(0)
            .LessThan(x => x.ValorTotal).WithMessage("Entrada não pode ser maior ou igual ao valor total.");
        RuleFor(x => x.NumeroParcelas).InclusiveBetween(1, 60);
        RuleFor(x => x.TaxaJurosMensal).GreaterThanOrEqualTo(0);
    }
}

public class AbrirCrediarioHandler(
    ICrediarioRepository repo,
    ILancamentoFinanceiroRepository lancRepo,
    IClienteRepository clienteRepo,
    IUnitOfWork uow) : IRequestHandler<AbrirCrediarioCommand, AbrirCrediarioResult>
{
    public async Task<AbrirCrediarioResult> Handle(AbrirCrediarioCommand cmd, CancellationToken ct)
    {
        var numero = await repo.ProximoNumeroAsync(cmd.EmpresaId, ct);

        var crediario = CrediarioEntity.Criar(
            cmd.EmpresaId, cmd.ClienteId, cmd.UsuarioId,
            numero, cmd.ValorTotal, cmd.ValorEntrada,
            cmd.NumeroParcelas, cmd.TaxaJurosMensal,
            DateTime.Today, cmd.VendaId,
            cmd.DataPrimeiraParcela, cmd.DiaVencimento, cmd.Observacao);

        // Cada parcela do crediário vira um título em Contas a Receber, vinculado à
        // parcela — assim o Contas a Receber vira o painel único do que entra a prazo.
        var clienteNome = (await clienteRepo.ObterPorIdAsync(cmd.ClienteId, ct))?.Nome;
        foreach (var parcela in crediario.Parcelas)
        {
            var lanc = LancamentoFinanceiro.Criar(cmd.EmpresaId, TipoLancamento.ContaReceber,
                $"Crediário Nº {numero} - Parcela {parcela.Numero}/{cmd.NumeroParcelas}",
                parcela.Valor, parcela.DataVencimento,
                clienteId: cmd.ClienteId,
                documentoOrigem: $"Crediário {numero}",
                parcela: parcela.Numero, totalParcelas: cmd.NumeroParcelas,
                grupoParcelamento: crediario.Id.ToString());
            lanc.DefinirClassificacao("Crediário", clienteNome, cmd.Observacao);

            await lancRepo.AdicionarAsync(lanc, ct);
            parcela.VincularContaReceber(lanc.Id);
        }

        await repo.AdicionarAsync(crediario, ct);
        await uow.SalvarAsync(ct);

        var valorParcela = crediario.Parcelas.FirstOrDefault()?.Valor ?? 0;
        return new AbrirCrediarioResult(crediario.Id, numero, cmd.NumeroParcelas, valorParcela);
    }
}
