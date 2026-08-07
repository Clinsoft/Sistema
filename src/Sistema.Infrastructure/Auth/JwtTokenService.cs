using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Sistema.Domain.Auth;
using Sistema.Domain.Cadastros.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Sistema.Infrastructure.Auth;

public class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    public DateTime Expiracao => DateTime.UtcNow.AddHours(8);

    public string GerarToken(Usuario usuario)
    {
        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret não configurado.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? ""),
            new("empresaId", usuario.EmpresaId.ToString()),
            new(ClaimTypes.Role, usuario.Perfil ?? ""),
            new("nome", usuario.Nome)
        };
        // Loja (unidade) do colaborador — usada para limitar o atendente à sua loja.
        if (usuario.LocalEstoqueId.HasValue)
            claims.Add(new Claim("localEstoqueId", usuario.LocalEstoqueId.Value.ToString()));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: Expiracao,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
