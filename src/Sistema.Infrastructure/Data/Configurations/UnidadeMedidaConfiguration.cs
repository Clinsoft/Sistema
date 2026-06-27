using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class UnidadeMedidaConfiguration : IEntityTypeConfiguration<UnidadeMedida>
{
    public void Configure(EntityTypeBuilder<UnidadeMedida> b)
    {
        b.ToTable("UnidadesMedida");
        b.HasKey(u => u.Id);
        b.Property(u => u.Sigla).HasMaxLength(6).IsRequired();
        b.Property(u => u.Descricao).HasMaxLength(50).IsRequired();
        b.Property(u => u.Pesavel).HasDefaultValue(false);
        b.HasIndex(u => new { u.EmpresaId, u.Sigla }).IsUnique();
    }
}
