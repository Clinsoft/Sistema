using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Financeiro.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class OperadoraCartaoConfiguration : IEntityTypeConfiguration<OperadoraCartao>
{
    public void Configure(EntityTypeBuilder<OperadoraCartao> b)
    {
        b.ToTable("OperadorasCartao");
        b.HasKey(o => o.Id);
        b.Property(o => o.Nome).HasMaxLength(100).IsRequired();
        b.Property(o => o.Cor).HasMaxLength(20);
        b.Property(o => o.Icone).HasMaxLength(80);
        b.Property(o => o.BandeirasJson).HasColumnName("Bandeiras").HasMaxLength(500);
        b.Property(o => o.TaxaDebito).HasColumnType("decimal(6,4)");
        b.Property(o => o.TaxaCreditoVista).HasColumnType("decimal(6,4)");
        b.Property(o => o.TaxaCreditoParcelado).HasColumnType("decimal(6,4)");
        b.Property(o => o.TaxaPix).HasColumnType("decimal(6,4)");
        b.Property(o => o.TaxaAntecipacao).HasColumnType("decimal(6,4)");
        b.HasIndex(o => new { o.EmpresaId, o.Ativo });
    }
}

public class RecebivelCartaoConfiguration : IEntityTypeConfiguration<RecebivelCartao>
{
    public void Configure(EntityTypeBuilder<RecebivelCartao> b)
    {
        b.ToTable("ReceiveisCartao");
        b.HasKey(r => r.Id);
        b.Property(r => r.FormaPagamento).HasMaxLength(30).IsRequired();
        b.Property(r => r.ValorBruto).HasColumnType("decimal(18,2)");
        b.Property(r => r.Taxa).HasColumnType("decimal(6,4)");
        b.Property(r => r.ValorLiquido).HasColumnType("decimal(18,2)");
        b.Property(r => r.TaxaAntecipacaoAplicada).HasColumnType("decimal(6,4)");
        b.Property(r => r.NsuTid).HasMaxLength(50);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne(r => r.Operadora)
            .WithMany()
            .HasForeignKey(r => r.OperadoraCartaoId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.EmpresaId, r.Status, r.DataPrevistaRepasse });
    }
}
