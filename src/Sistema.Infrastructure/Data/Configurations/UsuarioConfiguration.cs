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

        // Dados de funcionário
        b.Property(u => u.Cpf).HasMaxLength(14);
        b.Property(u => u.Telefone).HasMaxLength(20);
        b.Property(u => u.Cargo).HasMaxLength(80);
        b.Property(u => u.Salario).HasColumnType("decimal(18,2)");
        b.Property(u => u.Observacao).HasMaxLength(500);

        // Acesso (opcional): e-mail/senha/perfil podem ser nulos para colaborador sem login.
        b.Property(u => u.Email).HasMaxLength(150);
        b.Property(u => u.SenhaHash).HasMaxLength(256);
        b.Property(u => u.Perfil).HasMaxLength(30);

        // Índice único de e-mail só entre quem tem e-mail (permite vários colaboradores sem login).
        b.HasIndex(u => new { u.EmpresaId, u.Email })
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL");
    }
}
