using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Cadastros.Interfaces;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Cadastros;

/// <summary>
/// Cadastro de contadores (contabilidade). Cada contador é um login (Perfil "Contador")
/// separado dos colaboradores, opcionalmente vinculado ao fornecedor de honorários.
/// </summary>
[ApiController]
[Route("api/contadores")]
[Authorize(Roles = "Administrador")]
public class ContadoresController(SistemaDbContext db, IUsuarioRepository repo, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Perfil == "Contador")
            .Select(u => new
            {
                u.Id, u.Nome, u.Email, u.Telefone, u.Ativo, u.UltimoAcesso, u.FornecedorId,
                fornecedorNome = db.Fornecedores.Where(f => f.Id == u.FornecedorId)
                    .Select(f => f.RazaoSocial).FirstOrDefault(),
                temAcesso = u.Email != null && u.SenhaHash != null
            })
            .OrderBy(u => u.Nome)
            .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] SalvarContadorRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nome)) return BadRequest(new { mensagem = "Informe o nome." });
        if (string.IsNullOrWhiteSpace(req.Email)) return BadRequest(new { mensagem = "Informe o e-mail de acesso." });
        if (string.IsNullOrWhiteSpace(req.Senha) || req.Senha.Length < 6)
            return BadRequest(new { mensagem = "A senha deve ter ao menos 6 caracteres." });

        var emailEmUso = await db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.EmpresaId == req.EmpresaId && u.Email == req.Email, ct);
        if (emailEmUso) return BadRequest(new { mensagem = "Já existe um acesso com este e-mail." });

        var contador = Usuario.CriarColaborador(req.EmpresaId, req.Nome, telefone: req.Telefone);
        contador.ConcederAcesso(req.Email, BCrypt.Net.BCrypt.HashPassword(req.Senha), "Contador");
        contador.VincularFornecedor(req.FornecedorId);

        await repo.AdicionarAsync(contador, ct);
        await uow.SalvarAsync(ct);
        return Ok(new { contador.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] SalvarContadorRequest req, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");

        c.EditarDadosColaborador(req.Nome, c.Cpf, req.Telefone, c.Cargo, c.Salario, c.DataAdmissao, c.Observacao);
        c.VincularFornecedor(req.FornecedorId);

        // Senha opcional na edição (reset).
        if (!string.IsNullOrWhiteSpace(req.Senha))
        {
            if (req.Senha.Length < 6) return BadRequest(new { mensagem = "A senha deve ter ao menos 6 caracteres." });
            c.AlterarSenha(BCrypt.Net.BCrypt.HashPassword(req.Senha));
        }

        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/senha")]
    public async Task<IActionResult> AlterarSenha(Guid id, [FromBody] AlterarSenhaContadorRequest req, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        if (string.IsNullOrWhiteSpace(req.NovaSenha) || req.NovaSenha.Length < 6)
            return BadRequest(new { mensagem = "A senha deve ter ao menos 6 caracteres." });
        c.AlterarSenha(BCrypt.Net.BCrypt.HashPassword(req.NovaSenha));
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        c.Desativar();
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        var c = await repo.ObterPorIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        c.Reativar();
        repo.Atualizar(c);
        await uow.SalvarAsync(ct);
        return NoContent();
    }
}

public record SalvarContadorRequest(
    Guid EmpresaId, string Nome, string Email,
    string? Senha = null, string? Telefone = null, Guid? FornecedorId = null);
public record AlterarSenhaContadorRequest(string NovaSenha);
