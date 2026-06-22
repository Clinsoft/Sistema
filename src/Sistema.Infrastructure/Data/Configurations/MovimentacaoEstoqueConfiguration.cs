using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class MovimentacaoEstoqueConfiguration : IEntityTypeConfiguration<MovimentacaoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentacaoEstoque> b)
    {
        b.ToTable("MovimentacoesEstoque");
        b.HasKey(m => m.Id);
        b.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.Quantidade).HasColumnType("decimal(18,3)");
        b.Property(m => m.CustoUnitario).HasColumnType("decimal(18,4)");
        b.Property(m => m.DocumentoOrigem).HasMaxLength(50);
        b.Property(m => m.Observacao).HasMaxLength(300);
        b.HasIndex(m => new { m.EmpresaId, m.CriadoEm });
        b.HasIndex(m => new { m.ProdutoId, m.CriadoEm });
    }
}
