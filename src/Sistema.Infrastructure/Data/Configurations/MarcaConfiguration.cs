using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class MarcaConfiguration : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> b)
    {
        b.ToTable("Marcas");
        b.HasKey(m => m.Id);
        b.Property(m => m.Nome).HasMaxLength(100).IsRequired();
    }
}
