using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class PDVSessaoConfiguration : IEntityTypeConfiguration<PDVSessao>
{
    public void Configure(EntityTypeBuilder<PDVSessao> b)
    {
        b.ToTable("PDVSessoes");
        b.HasKey(s => s.Id);
        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(10);
        b.Property(s => s.SaldoAbertura).HasColumnType("decimal(18,2)");
        b.Property(s => s.SaldoFechamento).HasColumnType("decimal(18,2)");
        b.Property(s => s.TotalVendas).HasColumnType("decimal(18,2)");
        b.Property(s => s.TotalSuprimentos).HasColumnType("decimal(18,2)");
        b.Property(s => s.TotalSangrias).HasColumnType("decimal(18,2)");
        b.Property(s => s.ObservacaoFechamento).HasMaxLength(500);
        b.HasIndex(s => new { s.EmpresaId, s.Abertura });
    }
}
