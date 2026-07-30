using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sistema.API.Controllers;

/// <summary>Ações administrativas do servidor (somente Administrador).</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Administrador")]
public class AdminController(ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>
    /// Reinicia o servidor: encerra o processo; o systemd (Restart=always) sobe
    /// de novo em ~2s. A resposta é enviada antes de o processo sair.
    /// </summary>
    [HttpPost("reiniciar")]
    public IActionResult Reiniciar()
    {
        logger.LogWarning("[ADMIN] Reinício do servidor solicitado por {User}.", User.Identity?.Name ?? "?");
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);          // deixa a resposta HTTP concluir
            Environment.Exit(0);            // systemd reinicia (Restart=always)
        });
        return Ok(new { mensagem = "Servidor reiniciando… volta em alguns segundos." });
    }
}
