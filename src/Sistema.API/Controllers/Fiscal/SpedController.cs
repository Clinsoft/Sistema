using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sistema.Infrastructure.Fiscal;

namespace Sistema.API.Controllers.Fiscal;

[ApiController]
[Route("api/fiscal/sped")]
[Authorize(Roles = "Administrador,Contador")]
public class SpedController(SpedFiscalService spedService) : ControllerBase
{
    /// <summary>Gera e baixa o arquivo EFD ICMS/IPI (SPED Fiscal) do mês.</summary>
    [HttpGet("efd-icms")]
    public async Task<IActionResult> DownloadEfdIcms(
        [FromQuery] Guid empresaId, [FromQuery] int ano, [FromQuery] int mes,
        CancellationToken ct)
    {
        var bytes = await spedService.GerarAsync(empresaId, ano, mes, ct);
        var nomeArquivo = $"SPED_EFD_ICMS_{ano}{mes:D2}.txt";
        return File(bytes, "text/plain", nomeArquivo);
    }
}
