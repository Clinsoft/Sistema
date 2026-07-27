using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Auth;

/// <summary>
/// Cadastro de colaboradores/funcionários. Todo colaborador tem dados de RH
/// (cargo, salário, CPF…) e, OPCIONALMENTE, acesso ao sistema (login). A tabela
/// segue como "Usuarios".
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "Administrador")]
public class UsuariosController(SistemaDbContext db, IUsuarioRepository repo, IUnitOfWork uow) : ControllerBase
{
    private static readonly string[] PerfisValidos = ["Administrador", "Atendente", "Financeiro", "Contador"];

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Perfil != "Contador")   // contadores têm tela própria
            .Select(u => new
            {
                u.Id, u.Nome, u.Cpf, u.Telefone, u.Cargo, u.Salario, u.DataAdmissao, u.Observacao,
                u.Email, u.Perfil, temAcesso = u.Email != null && u.SenhaHash != null,
                ehCliente = u.Cpf != null && db.Clientes.Any(c => c.EmpresaId == u.EmpresaId && c.CpfCnpj == u.Cpf),
                u.Ativo, u.UltimoAcesso,
            })
            .OrderBy(u => u.Nome)
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] SalvarColaboradorRequest req, CancellationToken ct)
    {
        var colaborador = Usuario.CriarColaborador(req.EmpresaId, req.Nome,
            req.Cpf, req.Telefone, req.Cargo, req.Salario, req.DataAdmissao, req.Observacao);

        // Acesso opcional já na criação.
        if (req.Acesso is not null)
        {
            var erro = await ValidarEConcederAcessoAsync(colaborador, req.EmpresaId, req.Acesso, ct);
            if (erro is not null) return BadRequest(new { mensagem = erro });
        }

        await repo.AdicionarAsync(colaborador, ct);
        await uow.SalvarAsync(ct);
        return Ok(new { colaborador.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] SalvarColaboradorRequest req, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Colaborador não encontrado.");
        c.EditarDadosColaborador(req.Nome, req.Cpf, req.Telefone, req.Cargo, req.Salario, req.DataAdmissao, req.Observacao);
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Concede ou atualiza o acesso ao sistema (login) do colaborador.</summary>
    [HttpPost("{id:guid}/acesso")]
    public async Task<IActionResult> ConcederAcesso(Guid id, [FromBody] ConcederAcessoRequest req, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Colaborador não encontrado.");

        var erro = await ValidarEConcederAcessoAsync(c, c.EmpresaId,
            new AcessoDto(req.Email, req.Senha, req.Perfil), ct, ignorarId: id);
        if (erro is not null) return BadRequest(new { mensagem = erro });

        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Troca o perfil de acesso de um colaborador que já tem login.</summary>
    [HttpPatch("{id:guid}/perfil")]
    public async Task<IActionResult> AlterarPerfil(Guid id, [FromBody] AlterarPerfilRequest req, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Colaborador não encontrado.");
        if (!c.TemAcesso) return BadRequest(new { mensagem = "Colaborador não tem acesso ao sistema." });
        if (!PerfisValidos.Contains(req.Perfil)) return BadRequest(new { mensagem = "Perfil inválido." });
        c.AlterarPerfil(req.Perfil);
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>Remove o acesso ao sistema, mantendo o cadastro do colaborador.</summary>
    [HttpDelete("{id:guid}/acesso")]
    public async Task<IActionResult> RevogarAcesso(Guid id, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Colaborador não encontrado.");
        c.RevogarAcesso();
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/senha")]
    public async Task<IActionResult> AlterarSenha(Guid id, [FromBody] AlterarSenhaRequest req, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Colaborador não encontrado.");
        if (!c.TemAcesso) return BadRequest(new { mensagem = "Colaborador não tem acesso ao sistema." });
        c.AlterarSenha(BCrypt.Net.BCrypt.HashPassword(req.NovaSenha));
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Colaborador não encontrado.");
        c.Desativar();
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Colaborador não encontrado.");
        c.Reativar();
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    // Valida perfil/e-mail únicos e concede o acesso na entidade (não salva).
    private async Task<string?> ValidarEConcederAcessoAsync(
        Usuario c, Guid empresaId, AcessoDto acesso, CancellationToken ct, Guid? ignorarId = null)
    {
        if (string.IsNullOrWhiteSpace(acesso.Email)) return "Informe o e-mail de acesso.";
        if (string.IsNullOrWhiteSpace(acesso.Senha) || acesso.Senha.Length < 6)
            return "A senha deve ter ao menos 6 caracteres.";
        if (!PerfisValidos.Contains(acesso.Perfil))
            return "Perfil inválido. Use: Administrador, Atendente, Financeiro ou Contador.";

        var emailEmUso = await db.Usuarios.AsNoTracking().AnyAsync(u =>
            u.EmpresaId == empresaId && u.Email == acesso.Email && u.Id != ignorarId, ct);
        if (emailEmUso) return "Já existe um colaborador com este e-mail de acesso.";

        c.ConcederAcesso(acesso.Email, BCrypt.Net.BCrypt.HashPassword(acesso.Senha), acesso.Perfil);
        return null;
    }
}

public record AcessoDto(string Email, string Senha, string Perfil);
public record SalvarColaboradorRequest(
    Guid EmpresaId, string Nome,
    string? Cpf = null, string? Telefone = null, string? Cargo = null,
    decimal? Salario = null, DateTime? DataAdmissao = null, string? Observacao = null,
    AcessoDto? Acesso = null);
public record ConcederAcessoRequest(string Email, string Senha, string Perfil);
public record AlterarPerfilRequest(string Perfil);
public record AlterarSenhaRequest(string NovaSenha);
