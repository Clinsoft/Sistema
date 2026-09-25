using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Assinaturas;

namespace Sistema.Infrastructure.Data.Configurations;

public class AssinaturaConfiguration : IEntityTypeConfiguration<Assinatura>
{
    public void Configure(EntityTypeBuilder<Assinatura> b)
    {
        b.ToTable("Assinaturas");
        b.HasKey(a => a.Id);
        b.HasIndex(a => a.EmpresaId).IsUnique();   // 1 assinatura por empresa
        b.Property(a => a.Plano).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(a => a.Ciclo).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(a => a.Observacao).HasMaxLength(400);
    }
}
