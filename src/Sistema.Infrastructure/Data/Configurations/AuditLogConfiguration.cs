using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Auditoria.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Acao).HasMaxLength(20).IsRequired();
        b.Property(x => x.Entidade).HasMaxLength(80).IsRequired();
        b.Property(x => x.EntidadeId).HasMaxLength(50);
        b.Property(x => x.UsuarioNome).HasMaxLength(150);
        b.Property(x => x.Resumo).HasMaxLength(200);
        b.Property(x => x.Alteracoes).HasMaxLength(1000);
        b.Property(x => x.Ip).HasMaxLength(64);
        b.HasIndex(x => new { x.EmpresaId, x.DataHora });
        b.HasIndex(x => new { x.Entidade, x.EntidadeId });
        b.HasIndex(x => x.UsuarioId);
    }
}
