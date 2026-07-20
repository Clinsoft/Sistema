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

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email ?? ""),
            new Claim("empresaId", usuario.EmpresaId.ToString()),
            new Claim(ClaimTypes.Role, usuario.Perfil ?? ""),
            new Claim("nome", usuario.Nome)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: Expiracao,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
