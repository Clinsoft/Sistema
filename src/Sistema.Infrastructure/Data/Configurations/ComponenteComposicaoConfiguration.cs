using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class ComponenteComposicaoConfiguration : IEntityTypeConfiguration<ComponenteComposicao>
{
    public void Configure(EntityTypeBuilder<ComponenteComposicao> b)
    {
        b.ToTable("ComponentesComposicao");
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantidade).HasPrecision(18, 3);
        b.Property(x => x.Unidade).HasMaxLength(10);
        b.HasIndex(x => x.ProdutoCompostoId);
    }
}
