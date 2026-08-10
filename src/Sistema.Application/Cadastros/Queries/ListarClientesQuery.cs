using MediatR;
using Sistema.Application.Cadastros.DTOs;
using Sistema.Domain.Cadastros.Interfaces;

namespace Sistema.Application.Cadastros.Queries;

public record ListarClientesQuery(Guid EmpresaId, string? Termo = null, int Pagina = 1, int TamanhoPagina = 20)
    : IRequest<ListarClientesResult>;

public record ListarClientesResult(IReadOnlyList<ClienteDto> Itens, int Total);

public class ListarClientesHandler(IClienteRepository repo)
    : IRequestHandler<ListarClientesQuery, ListarClientesResult>
{
    public async Task<ListarClientesResult> Handle(ListarClientesQuery q, CancellationToken ct)
    {
        var clientes = await repo.PesquisarAsync(q.EmpresaId, q.Termo ?? "", q.Pagina, q.TamanhoPagina, ct);
        var total = await repo.ContarAtivosAsync(q.EmpresaId, ct);

        var dtos = clientes.Select(c => new ClienteDto(
            c.Id, c.Nome, c.CpfCnpj, c.TipoPessoa.ToString(),
            c.Email, c.Telefone, c.Celular, c.DataNascimento,
            c.Classificacao, c.Logradouro, c.Numero, c.Complemento,
            c.Bairro, c.Cidade, c.Uf, c.Cep,
            c.LimiteCredito, c.PontosFidelidade, c.Ativo, c.CriadoEm, c.LocalEstoqueId
        )).ToList();

        return new ListarClientesResult(dtos, total);
    }
}
