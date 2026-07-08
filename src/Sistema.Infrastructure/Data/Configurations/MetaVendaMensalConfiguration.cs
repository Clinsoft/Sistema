using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class MetaVendaMensalConfiguration : IEntityTypeConfiguration<MetaVendaMensal>
{
    public void Configure(EntityTypeBuilder<MetaVendaMensal> b)
    {
        b.ToTable("MetasVendaMensal");
        b.HasKey(m => m.Id);
        b.Property(m => m.Valor).HasColumnType("decimal(18,2)");
        b.HasIndex(m => new { m.EmpresaId, m.Ano, m.Mes }).IsUnique();
    }
}
