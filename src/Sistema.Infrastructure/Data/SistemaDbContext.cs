using Microsoft.EntityFrameworkCore;
using Sistema.Domain.Shared.Primitives;
using System.Reflection;

namespace Sistema.Infrastructure.Data;

public class SistemaDbContext(DbContextOptions<SistemaDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.GetType().GetProperty("AtualizadoEm")?.SetValue(entry.Entity, DateTime.UtcNow);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
