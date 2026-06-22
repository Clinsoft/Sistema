using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class LoteConfiguration : IEntityTypeConfiguration<Lote>
{
    public void Configure(EntityTypeBuilder<Lote> b)
    {
        b.ToTable("Lotes");
        b.HasKey(l => l.Id);
        b.Property(l => l.NumeroLote).HasMaxLength(50).IsRequired();
        b.Property(l => l.Quantidade).HasColumnType("decimal(18,3)");
        b.Property(l => l.CustoUnitario).HasColumnType("decimal(18,4)");
        b.HasIndex(l => new { l.ProdutoId, l.LocalEstoqueId, l.NumeroLote }).IsUnique();
    }
}
