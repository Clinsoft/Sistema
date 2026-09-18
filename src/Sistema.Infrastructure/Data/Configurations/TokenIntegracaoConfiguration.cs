using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class TokenIntegracaoConfiguration : IEntityTypeConfiguration<TokenIntegracao>
{
    public void Configure(EntityTypeBuilder<TokenIntegracao> b)
    {
        b.ToTable("TokensIntegracao");
        b.HasKey(t => t.Id);
        b.Property(t => t.Provedor).HasMaxLength(40).IsRequired();
        b.Property(t => t.AccessToken).HasMaxLength(2000).IsRequired();
        b.Property(t => t.RefreshToken).HasMaxLength(2000);
        b.Property(t => t.UsuarioExterno).HasMaxLength(80);
        b.HasIndex(t => t.Provedor).IsUnique();
    }
}
