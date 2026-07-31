using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema.Infrastructure.Data;

namespace Sistema.API.Controllers.Vendas;

/// <summary>
/// Lista enxuta de colaboradores para SELEÇÃO DO VENDEDOR no PDV. Acessível a
/// qualquer usuário logado (inclusive Atendente) — diferente de /usuarios, que é
/// restrito a Administrador. Expõe só id/nome/perfil, nada sensível.
/// </summary>
[ApiController]
[Route("api/vendedores")]
[Authorize]
public class VendedoresController(SistemaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid empresaId, CancellationToken ct)
        => Ok(await db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Ativo && u.Perfil != "Contador")
            .OrderBy(u => u.Nome)
            .Select(u => new { u.Id, u.Nome, perfil = u.Perfil, u.LocalEstoqueId })
            .ToListAsync(ct));
}
