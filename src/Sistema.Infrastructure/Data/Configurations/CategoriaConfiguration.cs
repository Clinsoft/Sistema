using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.ToTable("Categorias");
        b.HasKey(c => c.Id);
        b.Property(c => c.Nome).HasMaxLength(100).IsRequired();
    }
}
