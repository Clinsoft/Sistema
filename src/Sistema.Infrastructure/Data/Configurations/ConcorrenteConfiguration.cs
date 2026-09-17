using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class ConcorrenteConfiguration : IEntityTypeConfiguration<Concorrente>
{
    public void Configure(EntityTypeBuilder<Concorrente> b)
    {
        b.ToTable("Concorrentes");
        b.HasKey(c => c.Id);
        b.Property(c => c.Nome).HasMaxLength(200).IsRequired();
        b.Property(c => c.Categoria).HasMaxLength(80);
        b.Property(c => c.Endereco).HasMaxLength(300);
        b.Property(c => c.Telefone).HasMaxLength(40);
        b.Property(c => c.Website).HasMaxLength(300);
        b.Property(c => c.Fonte).HasMaxLength(20).IsRequired();
        b.Property(c => c.OsmRef).HasMaxLength(40);
        b.Property(c => c.UrlOnline).HasMaxLength(300);
        b.Property(c => c.DistanciaKm).HasPrecision(6, 2);

        b.HasIndex(c => new { c.LocalEstoqueId, c.OsmRef });
        b.HasIndex(c => c.EmpresaId);
    }
}
