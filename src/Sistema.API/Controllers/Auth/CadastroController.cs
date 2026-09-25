using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Assinaturas;
using Sistema.Domain.Cadastros.Entities;
using Sistema.Domain.Estoque.Entities;
using Sistema.Infrastructure.Branding;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Auth;

/// <summary>
/// Auto-cadastro (self-service) de uma nova loja-cliente na instância SaaS. Cria empresa +
/// admin + loja principal + assinatura Trial (14 dias). Só disponível quando a instância tem
/// <c>Branding:AutoCadastro=true</c> (a EcoGranel, single-tenant, mantém false).
/// </summary>
[ApiController]
[Route("api/cadastro")]
[AllowAnonymous]
public class CadastroController(SistemaDbContext db, BrandingOptions branding) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("reset")]
    public async Task<IActionResult> Criar([FromBody] CadastroRequest req, CancellationToken ct)
    {
        if (!branding.AutoCadastro)
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Cadastro público não disponível nesta instalação." });

        if (string.IsNullOrWhiteSpace(req.NomeLoja)) return BadRequest(new { mensagem = "Informe o nome da loja." });
        if (string.IsNullOrWhiteSpace(req.NomeAdmin)) return BadRequest(new { mensagem = "Informe o seu nome." });
        if (string.IsNullOrWhiteSpace(req.EmailAdmin)) return BadRequest(new { mensagem = "Informe o e-mail de acesso." });
        if (req.SenhaAdmin is null || req.SenhaAdmin.Length < 6) return BadRequest(new { mensagem = "A senha deve ter ao menos 6 caracteres." });

        var cnpj = Raw(req.Cnpj);
        if (cnpj.Length is < 11 or > 14) return BadRequest(new { mensagem = "CNPJ inválido." });

        if (await db.Empresas.AnyAsync(e => e.Cnpj == cnpj, ct))
            return Conflict(new { mensagem = "Já existe uma conta com este CNPJ." });
        if (await db.Usuarios.AnyAsync(u => u.Email == req.EmailAdmin, ct))
            return Conflict(new { mensagem = "Este e-mail já está em uso. Faça login ou recupere o acesso." });

        var empresa = Empresa.Criar(
            razaoSocial: string.IsNullOrWhiteSpace(req.RazaoSocial) ? req.NomeLoja : req.RazaoSocial!,
            nomeFantasia: req.NomeLoja,
            cnpj: cnpj,
            regimeTributario: string.IsNullOrWhiteSpace(req.RegimeTributario) ? "SN" : req.RegimeTributario!,
            logradouro: "", numero: "", bairro: "",
            cidade: req.Cidade ?? "", uf: req.Uf ?? "", cep: "",
            telefone: req.Telefone ?? "", email: string.IsNullOrWhiteSpace(req.EmailLoja) ? req.EmailAdmin : req.EmailLoja!);
        db.Empresas.Add(empresa);

        var admin = Usuario.Criar(empresa.Id, req.NomeAdmin, req.EmailAdmin,
            BCrypt.Net.BCrypt.HashPassword(req.SenhaAdmin), "Administrador");
        db.Usuarios.Add(admin);

        // Loja principal padrão (o PDV precisa de um local de estoque para operar).
        db.LocaisEstoque.Add(LocalEstoque.Criar(empresa.Id, "Loja Principal", true, null));

        var plano = Enum.TryParse<PlanoAssinatura>(req.Plano, true, out var p) ? p : PlanoAssinatura.Profissional;
        db.Assinaturas.Add(Assinatura.CriarTrial(empresa.Id, plano));

        await db.SaveChangesAsync(ct);
        return Ok(new { empresaId = empresa.Id, mensagem = "Conta criada! Você tem 14 dias grátis para testar." });
    }

    private static string Raw(string? s)
        => new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

public record CadastroRequest(
    string NomeLoja,
    string Cnpj,
    string NomeAdmin,
    string EmailAdmin,
    string SenhaAdmin,
    string? Telefone = null,
    string? RazaoSocial = null,
    string? RegimeTributario = null,
    string? Cidade = null,
    string? Uf = null,
    string? EmailLoja = null,
    string? Plano = null);
