using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Marketing.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class TemplateMarketingConfiguration : IEntityTypeConfiguration<TemplateMarketing>
{
    public void Configure(EntityTypeBuilder<TemplateMarketing> b)
    {
        b.ToTable("TemplatesMarketing");
        b.HasKey(t => t.Id);
        b.Property(t => t.Nome).HasMaxLength(150).IsRequired();
        b.Property(t => t.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(t => t.Formato).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.LayoutJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(t => t.ThumbnailBase64).HasColumnType("nvarchar(max)");
    }
}

public class ArteMarketingConfiguration : IEntityTypeConfiguration<ArteMarketing>
{
    public void Configure(EntityTypeBuilder<ArteMarketing> b)
    {
        b.ToTable("ArtesMarketing");
        b.HasKey(a => a.Id);
        b.HasIndex(a => new { a.EmpresaId, a.CriadoEm });
        b.Property(a => a.Nome).HasMaxLength(150).IsRequired();
        b.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.Formato).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(15);
        b.Property(a => a.LayoutJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(a => a.ThumbnailBase64).HasColumnType("nvarchar(max)");
        b.Property(a => a.UrlExportada).HasMaxLength(500);
    }
}

public class AgendamentoPublicacaoConfiguration : IEntityTypeConfiguration<AgendamentoPublicacao>
{
    public void Configure(EntityTypeBuilder<AgendamentoPublicacao> b)
    {
        b.ToTable("AgendamentosPublicacao");
        b.HasKey(a => a.Id);
        b.HasIndex(a => new { a.EmpresaId, a.DataHoraAgendada, a.Status });
        b.Property(a => a.Rede).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(15);
        b.Property(a => a.Legenda).HasMaxLength(2200);
        b.Property(a => a.ErroPublicacao).HasMaxLength(500);
    }
}
