using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Ajuda.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class TutorialConfiguration : IEntityTypeConfiguration<Tutorial>
{
    public void Configure(EntityTypeBuilder<Tutorial> b)
    {
        b.ToTable("Tutoriais");
        b.HasKey(t => t.Id);
        b.HasIndex(t => new { t.EmpresaId, t.Ativo });
        b.Property(t => t.Titulo).HasMaxLength(150).IsRequired();
        b.Property(t => t.Descricao).HasColumnType("nvarchar(max)");
        b.Property(t => t.VideoUrl).HasMaxLength(500);
        b.Property(t => t.Categoria).HasMaxLength(60);
        b.Ignore(t => t.DomainEvents);
    }
}
