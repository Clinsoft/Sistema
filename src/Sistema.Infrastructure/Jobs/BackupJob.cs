using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sistema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sistema.Infrastructure.Jobs;

/// <summary>
/// Gera backup do banco SQL Server (BACKUP DATABASE) e mantém os últimos N dias.
/// Requer que o processo tenha permissão BACKUP DATABASE no SQL Server.
/// </summary>
public class BackupJob(SistemaDbContext db, IConfiguration config, ILogger<BackupJob> logger)
{
    [AutomaticRetry(Attempts = 2)]
    public async Task ExecutarAsync()
    {
        var pastaBackup = config["Backup:Pasta"] ?? @"C:\Backups\Clinsoft";
        var retemDias = int.TryParse(config["Backup:RetemDias"], out var d) ? d : 30;
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string não configurada.");

        // Extrai nome do banco da connection string
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        var nomeBanco = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(nomeBanco))
        {
            logger.LogError("[BACKUP] Não foi possível determinar o nome do banco da connection string.");
            return;
        }

        Directory.CreateDirectory(pastaBackup);

        var nomeArquivo = $"{nomeBanco}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        var caminhoArquivo = Path.Combine(pastaBackup, nomeArquivo);

        logger.LogInformation("[BACKUP] Iniciando backup de '{Banco}' para '{Arquivo}'", nomeBanco, caminhoArquivo);

        try
        {
            var sql = $"BACKUP DATABASE [{nomeBanco}] TO DISK = N'{caminhoArquivo}' WITH COMPRESSION, STATS = 10";
            await db.Database.ExecuteSqlRawAsync(sql);
            logger.LogInformation("[BACKUP] Concluído: {Arquivo}", caminhoArquivo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BACKUP] Falha ao executar BACKUP DATABASE");
            throw;
        }

        // Purgar backups antigos
        try
        {
            var limite = DateTime.Now.AddDays(-retemDias);
            foreach (var arquivo in Directory.GetFiles(pastaBackup, "*.bak"))
            {
                if (File.GetCreationTime(arquivo) < limite)
                {
                    File.Delete(arquivo);
                    logger.LogInformation("[BACKUP] Purga: {Arquivo}", arquivo);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[BACKUP] Falha ao purgar backups antigos (não crítico)");
        }
    }
}
