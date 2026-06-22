using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Cadastros.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.HasKey(u => u.Id);
        b.Property(u => u.Nome).HasMaxLength(100).IsRequired();
        b.Property(u => u.Email).HasMaxLength(150).IsRequired();
        b.HasIndex(u => new { u.EmpresaId, u.Email }).IsUnique();
        b.Property(u => u.SenhaHash).HasMaxLength(256).IsRequired();
        b.Property(u => u.Perfil).HasMaxLength(30).IsRequired();
    }
}
