using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class VendaConfiguration : IEntityTypeConfiguration<Venda>
{
    public void Configure(EntityTypeBuilder<Venda> b)
    {
        b.ToTable("Vendas");
        b.HasKey(v => v.Id);
        b.Property(v => v.Numero).HasMaxLength(20).IsRequired();
        b.HasIndex(v => new { v.EmpresaId, v.Numero }).IsUnique();
        b.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(v => v.SubTotal).HasColumnType("decimal(18,2)");
        b.Property(v => v.TotalDesconto).HasColumnType("decimal(18,2)");
        b.Property(v => v.TotalAcrescimo).HasColumnType("decimal(18,2)");
        b.Property(v => v.Total).HasColumnType("decimal(18,2)");
        b.Property(v => v.TotalPago).HasColumnType("decimal(18,2)");
        b.Property(v => v.Troco).HasColumnType("decimal(18,2)");
        b.Property(v => v.Observacao).HasMaxLength(500);
        b.HasIndex(v => new { v.EmpresaId, v.DataHora });
        b.HasIndex(v => new { v.EmpresaId, v.ClienteId });

        b.HasMany(v => v.Itens)
         .WithOne()
         .HasForeignKey(i => i.VendaId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(v => v.Pagamentos)
         .WithOne()
         .HasForeignKey(p => p.VendaId)
         .OnDelete(DeleteBehavior.Cascade);

        b.Ignore(v => v.DomainEvents);
    }
}

public class ItemVendaConfiguration : IEntityTypeConfiguration<ItemVenda>
{
    public void Configure(EntityTypeBuilder<ItemVenda> b)
    {
        b.ToTable("ItensVenda");
        b.HasKey(i => i.Id);
        b.Property(i => i.Descricao).HasMaxLength(200).IsRequired();
        b.Property(i => i.Quantidade).HasColumnType("decimal(18,3)");
        b.Property(i => i.PrecoUnitario).HasColumnType("decimal(18,2)");
        b.Property(i => i.PercentualDesconto).HasColumnType("decimal(5,2)");
        b.Property(i => i.TotalDesconto).HasColumnType("decimal(18,2)");
        b.Property(i => i.Total).HasColumnType("decimal(18,2)");
    }
}

public class PagamentoVendaConfiguration : IEntityTypeConfiguration<PagamentoVenda>
{
    public void Configure(EntityTypeBuilder<PagamentoVenda> b)
    {
        b.ToTable("PagamentosVenda");
        b.HasKey(p => p.Id);
        b.Property(p => p.Forma).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Valor).HasColumnType("decimal(18,2)");
        b.Property(p => p.Descricao).HasMaxLength(100);
    }
}
