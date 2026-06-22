using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class LocalEstoqueConfiguration : IEntityTypeConfiguration<LocalEstoque>
{
    public void Configure(EntityTypeBuilder<LocalEstoque> b)
    {
        b.ToTable("LocaisEstoque");
        b.HasKey(l => l.Id);
        b.Property(l => l.Nome).HasMaxLength(100).IsRequired();
        b.Property(l => l.Descricao).HasMaxLength(200);
    }
}
