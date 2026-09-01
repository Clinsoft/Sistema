using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> b)
    {
        b.ToTable("Produtos");
        b.HasKey(p => p.Id);
        b.Property(p => p.Codigo).HasMaxLength(30).IsRequired();
        b.HasIndex(p => new { p.EmpresaId, p.Codigo }).IsUnique();
        b.Property(p => p.CodigoBarras).HasMaxLength(30);
        // Único por empresa: um código de barras não pode se repetir em dois produtos
        // (evita estoque negativo e de-para ambíguo no PDV). NULL é ignorado pelo filtro.
        b.HasIndex(p => new { p.EmpresaId, p.CodigoBarras }).IsUnique().HasFilter("[CodigoBarras] IS NOT NULL");
        b.Property(p => p.CodigoFornecedorPrincipal).HasMaxLength(60);
        b.HasIndex(p => new { p.EmpresaId, p.FornecedorPrincipalId, p.CodigoFornecedorPrincipal })
            .HasFilter("[CodigoFornecedorPrincipal] IS NOT NULL");
        b.Property(p => p.Descricao).HasMaxLength(200).IsRequired();
        b.Property(p => p.DescricaoComplementar).HasMaxLength(500);
        b.Property(p => p.Ncm).HasMaxLength(8);
        b.Property(p => p.Cest).HasMaxLength(7);
        b.Property(p => p.CstIcms).HasMaxLength(3);
        b.Property(p => p.CsosnIcms).HasMaxLength(3);
        b.Property(p => p.CstPisCofins).HasMaxLength(2);
        b.Property(p => p.Cfop).HasMaxLength(4);
        b.Property(p => p.Origem).HasMaxLength(1);
        b.Property(p => p.CustoUnitario).HasColumnType("decimal(18,4)");
        b.Property(p => p.PrecoVenda).HasColumnType("decimal(18,2)");
        b.Property(p => p.PrecoAtacado).HasColumnType("decimal(18,2)");
        b.Property(p => p.Markup).HasColumnType("decimal(18,4)");
        b.Property(p => p.MargemLucro).HasColumnType("decimal(18,2)");
        b.Property(p => p.AliquotaIcms).HasColumnType("decimal(5,2)");
        b.Property(p => p.AliquotaPis).HasColumnType("decimal(5,2)");
        b.Property(p => p.AliquotaCofins).HasColumnType("decimal(5,2)");
        b.Property(p => p.EstoqueAtual).HasColumnType("decimal(18,3)");
        b.Property(p => p.EstoqueMinimo).HasColumnType("decimal(18,3)");
        b.Property(p => p.EstoqueMaximo).HasColumnType("decimal(18,3)");
    }
}
