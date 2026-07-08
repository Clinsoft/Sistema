using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Vendas.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class OperacaoCaixaConfiguration : IEntityTypeConfiguration<OperacaoCaixa>
{
    public void Configure(EntityTypeBuilder<OperacaoCaixa> b)
    {
        b.ToTable("OperacoesCaixa");
        b.HasKey(o => o.Id);
        b.Property(o => o.Tipo).HasConversion<string>().HasMaxLength(12);
        b.Property(o => o.Valor).HasColumnType("decimal(18,2)");
        b.Property(o => o.Descricao).HasMaxLength(300);
        b.HasIndex(o => new { o.EmpresaId, o.SessaoId });
    }
}
