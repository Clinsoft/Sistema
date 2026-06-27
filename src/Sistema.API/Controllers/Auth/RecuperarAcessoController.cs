using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Interfaces;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Auth;

[ApiController]
[Route("api/auth/recuperar-acesso")]
[AllowAnonymous]
public class RecuperarAcessoController(SistemaDbContext db, IEmailService email, IUnitOfWork uow) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Recuperar([FromBody] RecuperarAcessoRequest req, CancellationToken ct)
    {
        // Normaliza CNPJ: mantém apenas letras maiúsculas e dígitos (formato RF 2026 é alfanumérico)
        var cnpjLimpo = new string(req.Cnpj.ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c)).ToArray());

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.Cnpj == cnpjLimpo ||
                new string(e.Cnpj.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray()) == cnpjLimpo, ct);

        if (empresa is null)
            // Resposta genérica para não revelar se CNPJ existe
            return Ok(new { mensagem = "Se o CNPJ estiver cadastrado, você receberá um e-mail com as instruções." });

        // Busca usuário administrador da empresa
        var usuario = await db.Usuarios
            .Where(u => u.EmpresaId == empresa.Id && u.Ativo && u.Perfil == "Administrador")
            .OrderBy(u => u.CriadoEm)
            .FirstOrDefaultAsync(ct);

        if (usuario is null)
            return Ok(new { mensagem = "Se o CNPJ estiver cadastrado, você receberá um e-mail com as instruções." });

        // Gera nova senha aleatória
        var novaSenha = GerarSenha();
        var novoHash = BCrypt.Net.BCrypt.HashPassword(novaSenha);
        usuario.AlterarSenha(novoHash);
        await uow.SalvarAsync(ct);

        // Monta e-mail
        var corpo = $"""
            <div style="font-family:sans-serif;max-width:520px;margin:auto;padding:32px">
              <div style="text-align:center;margin-bottom:24px">
                <h2 style="color:#5a3e2b;margin:0">EcoGranel</h2>
                <p style="color:#777;margin:4px 0 0">Sistema de Gestão</p>
              </div>
              <div style="background:#f9f6f0;border-radius:12px;padding:24px">
                <h3 style="color:#5a3e2b;margin-top:0">Recuperação de Acesso</h3>
                <p>Olá, <strong>{usuario.Nome}</strong>!</p>
                <p>Recebemos uma solicitação de recuperação de acesso para o CNPJ <strong>{FormatarCnpj(cnpjLimpo)}</strong>.</p>
                <p>Suas novas credenciais de acesso são:</p>
                <table style="border-collapse:collapse;width:100%;margin:16px 0">
                  <tr>
                    <td style="padding:8px 12px;background:#fff;border-radius:8px 8px 0 0;border:1px solid #e0d8cc">
                      <strong>Usuário (e-mail):</strong>
                    </td>
                    <td style="padding:8px 12px;background:#fff;border-radius:8px 8px 0 0;border:1px solid #e0d8cc">
                      {usuario.Email}
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:8px 12px;background:#fff;border-top:0;border:1px solid #e0d8cc;border-radius:0 0 8px 8px">
                      <strong>Nova senha:</strong>
                    </td>
                    <td style="padding:8px 12px;background:#fff;border-top:0;border:1px solid #e0d8cc;border-radius:0 0 8px 8px;font-size:18px;letter-spacing:2px;font-weight:bold;color:#2e7d32">
                      {novaSenha}
                    </td>
                  </tr>
                </table>
                <p style="color:#d32f2f;font-size:13px">
                  ⚠️ Por segurança, altere sua senha após o primeiro acesso nas Configurações do sistema.
                </p>
              </div>
              <p style="color:#aaa;font-size:12px;text-align:center;margin-top:24px">
                Se você não solicitou essa recuperação, ignore este e-mail. Nenhuma ação é necessária.
              </p>
            </div>
            """;

        await email.EnviarAsync(usuario.Email, "EcoGranel — Recuperação de Acesso", corpo, ct);

        return Ok(new { mensagem = "Se o CNPJ estiver cadastrado, você receberá um e-mail com as instruções." });
    }

    private static string GerarSenha()
    {
        const string letras = "abcdefghjkmnpqrstuvwxyz";
        const string maiusculas = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string numeros = "23456789";
        const string especiais = "@#!$";
        var rng = Random.Shared;

        var senha = new char[10];
        senha[0] = maiusculas[rng.Next(maiusculas.Length)];
        senha[1] = especiais[rng.Next(especiais.Length)];
        senha[2] = numeros[rng.Next(numeros.Length)];
        senha[3] = numeros[rng.Next(numeros.Length)];
        for (int i = 4; i < 10; i++)
            senha[i] = letras[rng.Next(letras.Length)];

        return new string(senha.OrderBy(_ => rng.Next()).ToArray());
    }

    private static string FormatarCnpj(string cnpj) =>
        cnpj.Length == 14
            ? $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}"
            : cnpj;
}

public record RecuperarAcessoRequest(string Cnpj);
