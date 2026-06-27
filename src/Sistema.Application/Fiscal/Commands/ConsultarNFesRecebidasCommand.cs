using MediatR;
using Sistema.Domain.Fiscal.Entities;

namespace Sistema.Application.Fiscal.Commands;

public record ConsultarNFesRecebidasCommand(Guid EmpresaId) : IRequest<ResultadoConsulta>;

public record ResultadoConsulta(bool Sucesso, string? Erro, int NovasNotas, int TotalNotas);

public record ManifestarNFeCommand(
    Guid EmpresaId,
    Guid NotaId,
    ManifestacaoTipo Tipo,
    string? Justificativa) : IRequest<bool>;
