using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class AtivoImobilizadoConfiguration : IEntityTypeConfiguration<AtivoImobilizado>
{
    public void Configure(EntityTypeBuilder<AtivoImobilizado> b)
    {
        b.ToTable("AtivosImobilizados");
        b.HasKey(a => a.Id);
        b.Property(a => a.Codigo).HasMaxLength(30).IsRequired();
        b.Property(a => a.Descricao).HasMaxLength(200).IsRequired();
        b.Property(a => a.Categoria).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.NotaFiscal).HasMaxLength(60);
        b.Property(a => a.NumeroSerie).HasMaxLength(60);
        b.Property(a => a.Localizacao).HasMaxLength(60);
        b.Property(a => a.MotivoBaixa).HasMaxLength(300);
        b.Property(a => a.Observacao).HasMaxLength(500);
        b.Property(a => a.ValorAquisicao).HasColumnType("decimal(18,2)");
        b.Property(a => a.ValorResidual).HasColumnType("decimal(18,2)");
        b.Property(a => a.Quantidade).HasColumnType("decimal(18,3)");
        b.HasIndex(a => new { a.EmpresaId, a.Codigo }).IsUnique();
        b.HasIndex(a => new { a.EmpresaId, a.Ativo });
        b.Ignore(a => a.DepreciacaoMensal);
    }
}
