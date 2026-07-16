using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class MaterialConsumoConfiguration : IEntityTypeConfiguration<MaterialConsumo>
{
    public void Configure(EntityTypeBuilder<MaterialConsumo> b)
    {
        b.ToTable("MateriaisConsumo");
        b.HasKey(m => m.Id);
        b.Property(m => m.Codigo).HasMaxLength(30).IsRequired();
        b.Property(m => m.Descricao).HasMaxLength(200).IsRequired();
        b.Property(m => m.CodigoFornecedor).HasMaxLength(60);
        b.Property(m => m.CodigoBarras).HasMaxLength(30);
        b.Property(m => m.Localizacao).HasMaxLength(60);
        b.Property(m => m.Observacao).HasMaxLength(500);
        b.Property(m => m.EstoqueAtual).HasColumnType("decimal(18,3)");
        b.Property(m => m.EstoqueMinimo).HasColumnType("decimal(18,3)");
        b.Property(m => m.CustoMedio).HasColumnType("decimal(18,4)");
        b.Property(m => m.UltimoCusto).HasColumnType("decimal(18,4)");
        b.HasIndex(m => new { m.EmpresaId, m.Codigo }).IsUnique();
        b.HasIndex(m => new { m.EmpresaId, m.Ativo });
        b.Ignore(m => m.ValorEmEstoque);
        b.Ignore(m => m.AbaixoDoMinimo);
    }
}

public class MovimentacaoMaterialConfiguration : IEntityTypeConfiguration<MovimentacaoMaterial>
{
    public void Configure(EntityTypeBuilder<MovimentacaoMaterial> b)
    {
        b.ToTable("MovimentacoesMaterial");
        b.HasKey(m => m.Id);
        b.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.Quantidade).HasColumnType("decimal(18,3)");
        b.Property(m => m.CustoUnitario).HasColumnType("decimal(18,4)");
        b.Property(m => m.DocumentoOrigem).HasMaxLength(60);
        b.Property(m => m.Observacao).HasMaxLength(300);
        b.HasIndex(m => new { m.EmpresaId, m.CriadoEm });
        b.HasIndex(m => new { m.MaterialConsumoId, m.CriadoEm });
        b.Ignore(m => m.QuantidadeComSinal);
        b.Ignore(m => m.ValorTotal);
    }
}
