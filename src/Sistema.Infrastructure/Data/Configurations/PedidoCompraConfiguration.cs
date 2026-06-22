using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Compras.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class PedidoCompraConfiguration : IEntityTypeConfiguration<PedidoCompra>
{
    public void Configure(EntityTypeBuilder<PedidoCompra> b)
    {
        b.ToTable("PedidosCompra");
        b.HasKey(p => p.Id);
        b.Property(p => p.Numero).HasMaxLength(20).IsRequired();
        b.HasIndex(p => new { p.EmpresaId, p.Numero }).IsUnique();
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(15);
        b.Property(p => p.Total).HasColumnType("decimal(18,2)");
        b.Property(p => p.Observacao).HasMaxLength(500);

        b.HasMany(p => p.Itens)
         .WithOne()
         .HasForeignKey(i => i.PedidoCompraId)
         .OnDelete(DeleteBehavior.Cascade);

        b.Ignore(p => p.DomainEvents);
    }
}

public class ItemPedidoCompraConfiguration : IEntityTypeConfiguration<ItemPedidoCompra>
{
    public void Configure(EntityTypeBuilder<ItemPedidoCompra> b)
    {
        b.ToTable("ItensPedidoCompra");
        b.HasKey(i => i.Id);
        b.Property(i => i.Descricao).HasMaxLength(200).IsRequired();
        b.Property(i => i.Quantidade).HasColumnType("decimal(18,3)");
        b.Property(i => i.PrecoUnitario).HasColumnType("decimal(18,4)");
        b.Property(i => i.Total).HasColumnType("decimal(18,2)");
        b.Property(i => i.QuantidadeRecebida).HasColumnType("decimal(18,3)");
    }
}
