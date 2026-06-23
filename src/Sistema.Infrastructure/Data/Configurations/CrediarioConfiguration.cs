using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Crediario.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class CrediarioConfiguration : IEntityTypeConfiguration<Crediario>
{
    public void Configure(EntityTypeBuilder<Crediario> b)
    {
        b.ToTable("Crediarios");
        b.HasKey(c => c.Id);
        b.HasIndex(c => new { c.EmpresaId, c.Numero }).IsUnique();
        b.HasIndex(c => new { c.EmpresaId, c.ClienteId });
        b.Property(c => c.Numero).HasMaxLength(20).IsRequired();
        b.Property(c => c.ValorTotal).HasColumnType("decimal(18,2)");
        b.Property(c => c.ValorEntrada).HasColumnType("decimal(18,2)");
        b.Property(c => c.ValorFinanciado).HasColumnType("decimal(18,2)");
        b.Property(c => c.TaxaJurosMensal).HasColumnType("decimal(8,4)");
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(15);

        b.HasMany(c => c.Parcelas)
         .WithOne()
         .HasForeignKey(p => p.CrediarioId)
         .OnDelete(DeleteBehavior.Cascade);

        b.Ignore(c => c.DomainEvents);
    }
}

public class ParcelaCrediarioConfiguration : IEntityTypeConfiguration<ParcelaCrediario>
{
    public void Configure(EntityTypeBuilder<ParcelaCrediario> b)
    {
        b.ToTable("ParcelasCrediario");
        b.HasKey(p => p.Id);
        b.HasIndex(p => new { p.CrediarioId, p.Numero }).IsUnique();
        b.HasIndex(p => new { p.CrediarioId, p.DataVencimento });
        b.Property(p => p.Valor).HasColumnType("decimal(18,2)");
        b.Property(p => p.ValorJuros).HasColumnType("decimal(18,2)");
        b.Property(p => p.ValorMulta).HasColumnType("decimal(18,2)");
        b.Property(p => p.ValorPago).HasColumnType("decimal(18,2)");
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(15);
        b.Property(p => p.PixCopiaCola).HasMaxLength(500);
    }
}

public class RenegociacaoCrediarioConfiguration : IEntityTypeConfiguration<RenegociacaoCrediario>
{
    public void Configure(EntityTypeBuilder<RenegociacaoCrediario> b)
    {
        b.ToTable("RenegociacoesCrediario");
        b.HasKey(r => r.Id);
        b.Property(r => r.SaldoRenegociado).HasColumnType("decimal(18,2)");
        b.Property(r => r.Desconto).HasColumnType("decimal(18,2)");
        b.Property(r => r.Motivo).HasMaxLength(500);
    }
}
