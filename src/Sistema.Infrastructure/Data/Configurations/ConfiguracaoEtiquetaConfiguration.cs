using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class ConfiguracaoEtiquetaConfiguration : IEntityTypeConfiguration<ConfiguracaoEtiqueta>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoEtiqueta> b)
    {
        b.ToTable("ConfiguracoesEtiqueta");
        b.HasKey(c => c.Id);
        b.Property(c => c.Template).HasMaxLength(40).IsRequired();
        b.Property(c => c.ConfigJson).IsRequired();   // nvarchar(max)
        b.HasIndex(c => new { c.EmpresaId, c.Template }).IsUnique();
    }
}
