using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Contabilidade.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class ContaContabilConfiguration : IEntityTypeConfiguration<ContaContabil>
{
    public void Configure(EntityTypeBuilder<ContaContabil> b)
    {
        b.ToTable("PlanoContas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
    }
}

public class LancamentoContabilConfiguration : IEntityTypeConfiguration<LancamentoContabil>
{
    public void Configure(EntityTypeBuilder<LancamentoContabil> b)
    {
        b.ToTable("LancamentosContabeis");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).HasMaxLength(20).IsRequired();
        b.Property(x => x.Historico).HasMaxLength(500).IsRequired();
        b.Property(x => x.DocumentoOrigem).HasMaxLength(60);

        b.HasMany(x => x.Partidas).WithOne().HasForeignKey(p => p.LancamentoContabilId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.EmpresaId, x.DataCompetencia });
        b.HasIndex(x => new { x.EmpresaId, x.Numero });
    }
}

public class PartidaContabilConfiguration : IEntityTypeConfiguration<PartidaContabil>
{
    public void Configure(EntityTypeBuilder<PartidaContabil> b)
    {
        b.ToTable("PartidasContabeis");
        b.HasKey(x => x.Id);
        b.Property(x => x.Valor).HasColumnType("decimal(18,2)");
        b.Property(x => x.Complemento).HasMaxLength(200);
    }
}
