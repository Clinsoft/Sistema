using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Auth;

[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public class SetupController(SistemaDbContext db) : ControllerBase
{
    /// <summary>Verifica se o sistema ainda precisa de configuração inicial.</summary>
    [HttpGet("necessario")]
    public async Task<IActionResult> Necessario(CancellationToken ct)
    {
        var temEmpresa = await db.Empresas.AnyAsync(ct);
        return Ok(new { necessario = !temEmpresa });
    }

    /// <summary>
    /// Cria a empresa e o primeiro usuário administrador.
    /// Só funciona se não houver nenhuma empresa cadastrada.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Configurar([FromBody] SetupRequest req, CancellationToken ct)
    {
        if (await db.Empresas.AnyAsync(ct))
            return Conflict(new { mensagem = "Sistema já configurado. Use as configurações para alterar os dados." });

        // Cria empresa
        var empresa = Empresa.Criar(
            razaoSocial: req.RazaoSocial,
            nomeFantasia: req.NomeFantasia,
            cnpj: CnpjRaw(req.Cnpj),   // remove pontuação; preserva letras (CNPJ alfanum. IN RFB 2.229/2024)
            regimeTributario: req.RegimeTributario,
            logradouro: req.Logradouro,
            numero: req.Numero,
            bairro: req.Bairro,
            cidade: req.Cidade,
            uf: req.Uf,
            cep: req.Cep.Replace("-", ""),
            telefone: req.Telefone,
            email: req.EmailEmpresa,
            complemento: req.Complemento);

        db.Empresas.Add(empresa);

        // Cria usuário admin
        var senhaHash = BCrypt.Net.BCrypt.HashPassword(req.SenhaAdmin);
        var admin = Usuario.Criar(empresa.Id, req.NomeAdmin, req.EmailAdmin, senhaHash, "Administrador");
        db.Usuarios.Add(admin);

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            empresaId = empresa.Id,
            mensagem = "Sistema configurado com sucesso! Faça login para continuar."
        });
    }

    // Remove pontuação do CNPJ preservando letras do novo formato alfanumérico (IN RFB 2.229/2024)
    private static string CnpjRaw(string cnpj) =>
        cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();
}

public record SetupRequest(
    // Empresa
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string RegimeTributario,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Uf,
    string Cep,
    string Telefone,
    string EmailEmpresa,
    // Admin
    string NomeAdmin,
    string EmailAdmin,
    string SenhaAdmin
);
