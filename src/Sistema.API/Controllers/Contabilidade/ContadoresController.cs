using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Contabilidade.Entities;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Contabilidade;

[ApiController]
[Route("api/contabilidade/contadores")]
[Authorize]
public class ContadoresController(SistemaDbContext db, IUnitOfWork uow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
    {
        var lista = await db.Contadores.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId)
            .OrderBy(c => c.Nome)
            .Select(c => new
            {
                c.Id, c.Nome, c.CpfCnpj, c.Email, c.Telefone, c.CRC, c.Ativo, c.CriadoEm, c.FornecedorId,
                fornecedorNome = db.Fornecedores.Where(f => f.Id == c.FornecedorId)
                    .Select(f => f.RazaoSocial).FirstOrDefault(),
                // Tem login ativo se existe um Usuario com o mesmo e-mail e senha definida.
                temAcesso = db.Usuarios.Any(u => u.EmpresaId == empresaId && u.Email == c.Email && u.SenhaHash != null)
            })
            .ToListAsync(ct);
        return Ok(lista);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] ContadorRequest req, CancellationToken ct)
    {
        var cpfCnpjLimpo = req.CpfCnpj.Replace(".", "").Replace("-", "").Replace("/", "");

        if (await db.Contadores.AnyAsync(c => c.EmpresaId == req.EmpresaId && c.CpfCnpj == cpfCnpjLimpo, ct))
            return BadRequest("Já existe um contador cadastrado com este CPF/CNPJ.");

        var contador = Contador.Criar(req.EmpresaId, req.Nome, req.CpfCnpj, req.Email,
            req.Telefone, req.CRC, req.FornecedorId);
        db.Contadores.Add(contador);

        var erroLogin = await GarantirLoginAsync(req.EmpresaId, req.Nome, req.Email, req.Senha, ct);
        if (erroLogin is not null) return BadRequest(erroLogin);

        await uow.SalvarAsync(ct);
        return Ok(new { contador.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] ContadorEditarRequest req, CancellationToken ct)
    {
        var contador = await db.Contadores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        contador.Editar(req.Nome, req.Email, req.Telefone, req.CRC, req.FornecedorId);
        db.Contadores.Update(contador);

        var erroLogin = await GarantirLoginAsync(contador.EmpresaId, req.Nome, req.Email, req.Senha, ct);
        if (erroLogin is not null) return BadRequest(erroLogin);

        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var contador = await db.Contadores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        contador.Desativar();
        db.Contadores.Update(contador);
        // Desativa também o login, se houver.
        var login = await db.Usuarios.FirstOrDefaultAsync(u => u.EmpresaId == contador.EmpresaId && u.Email == contador.Email, ct);
        login?.Desativar();
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        var contador = await db.Contadores.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Contador não encontrado.");
        contador.Reativar();
        db.Contadores.Update(contador);
        var login = await db.Usuarios.FirstOrDefaultAsync(u => u.EmpresaId == contador.EmpresaId && u.Email == contador.Email, ct);
        login?.Reativar();
        await uow.SalvarAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Garante o login (Usuario, perfil "Contador") do contador. Cria se não existir e
    /// houver senha; se já existe e veio senha, atualiza a senha. Sem senha e sem login,
    /// não faz nada (contador fica cadastrado, mas sem acesso).
    /// </summary>
    private async Task<string?> GarantirLoginAsync(Guid empresaId, string nome, string email, string? senha, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Informe o e-mail.";

        var user = await db.Usuarios.FirstOrDefaultAsync(u => u.EmpresaId == empresaId && u.Email == email, ct);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(senha)) return null;   // sem senha → não cria login agora
            if (senha.Length < 6) return "A senha deve ter ao menos 6 caracteres.";
            var novo = Usuario.CriarColaborador(empresaId, nome);
            novo.ConcederAcesso(email, BCrypt.Net.BCrypt.HashPassword(senha), "Contador");
            db.Usuarios.Add(novo);
        }
        else if (!string.IsNullOrWhiteSpace(senha))
        {
            if (senha.Length < 6) return "A senha deve ter ao menos 6 caracteres.";
            user.AlterarSenha(BCrypt.Net.BCrypt.HashPassword(senha));
            if (user.Perfil != "Contador") user.AlterarPerfil("Contador");
        }
        return null;
    }
}

public record ContadorRequest(Guid EmpresaId, string Nome, string CpfCnpj, string Email,
    string? Telefone = null, string? CRC = null, Guid? FornecedorId = null, string? Senha = null);
public record ContadorEditarRequest(string Nome, string Email, string? Telefone, string? CRC,
    Guid? FornecedorId = null, string? Senha = null);
