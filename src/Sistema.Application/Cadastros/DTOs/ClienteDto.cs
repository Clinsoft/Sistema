using Sistema.Domain.Cadastros.Entities;

namespace Sistema.Application.Cadastros.DTOs;

public record ClienteDto(
    Guid Id, string Nome, string? CpfCnpj, string TipoPessoa,
    string? Email, string? Telefone, string? Celular,
    DateTime? DataNascimento, string? Classificacao,
    string? Logradouro, string? Numero, string? Complemento,
    string? Bairro, string? Cidade, string? Uf, string? Cep,
    decimal LimiteCredito, int PontosFidelidade, bool Ativo, DateTime CriadoEm);

public record FornecedorDto(
    Guid Id, string RazaoSocial, string? NomeFantasia, string Cnpj,
    string? Email, string? Telefone, string? Contato,
    string? Cidade, string? Uf, int PrazoPagamentoDias, bool Ativo);
