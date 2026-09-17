using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class PrecoConcorrenteConfiguration : IEntityTypeConfiguration<PrecoConcorrente>
{
    public void Configure(EntityTypeBuilder<PrecoConcorrente> b)
    {
        b.ToTable("PrecosConcorrente");
        b.HasKey(p => p.Id);
        b.Property(p => p.Descricao).HasMaxLength(200).IsRequired();
        b.Property(p => p.Ean).HasMaxLength(20);
        b.Property(p => p.Unidade).HasMaxLength(12).IsRequired();
        b.Property(p => p.Observacao).HasMaxLength(300);
        b.Property(p => p.Preco).HasPrecision(12, 2);
        b.HasIndex(p => p.ConcorrenteId);
        b.HasIndex(p => p.ProdutoId);
    }
}
