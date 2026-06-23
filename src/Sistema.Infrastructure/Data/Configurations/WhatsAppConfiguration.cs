using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.WhatsApp.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class CatalogoWhatsAppConfiguration : IEntityTypeConfiguration<CatalogoWhatsApp>
{
    public void Configure(EntityTypeBuilder<CatalogoWhatsApp> b)
    {
        b.ToTable("CatalogosWhatsApp");
        b.HasKey(c => c.Id);
        b.Property(c => c.Nome).HasMaxLength(100).IsRequired();
        b.Property(c => c.Descricao).HasMaxLength(300);
        b.Property(c => c.WhatsAppBusinessId).HasMaxLength(100);
        b.Property(c => c.CatalogId).HasMaxLength(100);
        b.Property(c => c.Provedor).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.ApiToken).HasMaxLength(500);
        b.Property(c => c.WebhookUrl).HasMaxLength(300);

        b.HasMany(c => c.Itens)
         .WithOne()
         .HasForeignKey(i => i.CatalogoId)
         .OnDelete(DeleteBehavior.Cascade);

        b.Ignore(c => c.DomainEvents);
    }
}

public class ItemCatalogoConfiguration : IEntityTypeConfiguration<ItemCatalogo>
{
    public void Configure(EntityTypeBuilder<ItemCatalogo> b)
    {
        b.ToTable("ItensCatalogo");
        b.HasKey(i => i.Id);
        b.HasIndex(i => new { i.CatalogoId, i.ProdutoId }).IsUnique();
        b.Property(i => i.Descricao).HasMaxLength(300).IsRequired();
        b.Property(i => i.Preco).HasColumnType("decimal(18,2)");
        b.Property(i => i.UrlFoto).HasMaxLength(500);
        b.Property(i => i.IdExterno).HasMaxLength(100);
    }
}

public class PedidoWhatsAppConfiguration : IEntityTypeConfiguration<PedidoWhatsApp>
{
    public void Configure(EntityTypeBuilder<PedidoWhatsApp> b)
    {
        b.ToTable("PedidosWhatsApp");
        b.HasKey(p => p.Id);
        b.HasIndex(p => new { p.EmpresaId, p.Numero }).IsUnique();
        b.HasIndex(p => new { p.EmpresaId, p.CriadoEm });
        b.Property(p => p.TelefoneCliente).HasMaxLength(20).IsRequired();
        b.Property(p => p.NomeCliente).HasMaxLength(100).IsRequired();
        b.Property(p => p.Numero).HasMaxLength(20).IsRequired();
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(25);
        b.Property(p => p.TipoEntrega).HasConversion<string>().HasMaxLength(15);
        b.Property(p => p.StatusPagamento).HasConversion<string>().HasMaxLength(15);
        b.Property(p => p.Total).HasColumnType("decimal(18,2)");
        b.Property(p => p.Observacao).HasMaxLength(500);
        b.Property(p => p.EnderecoEntrega).HasMaxLength(300);
        b.Property(p => p.PixCopiaCola).HasMaxLength(500);

        b.HasMany(p => p.Itens)
         .WithOne()
         .HasForeignKey(i => i.PedidoWhatsAppId)
         .OnDelete(DeleteBehavior.Cascade);

        b.Ignore(p => p.DomainEvents);
    }
}

public class ItemPedidoWhatsAppConfiguration : IEntityTypeConfiguration<ItemPedidoWhatsApp>
{
    public void Configure(EntityTypeBuilder<ItemPedidoWhatsApp> b)
    {
        b.ToTable("ItensPedidoWhatsApp");
        b.HasKey(i => i.Id);
        b.Property(i => i.Descricao).HasMaxLength(200).IsRequired();
        b.Property(i => i.Quantidade).HasColumnType("decimal(18,3)");
        b.Property(i => i.PrecoUnitario).HasColumnType("decimal(18,2)");
        b.Property(i => i.Total).HasColumnType("decimal(18,2)");
    }
}
