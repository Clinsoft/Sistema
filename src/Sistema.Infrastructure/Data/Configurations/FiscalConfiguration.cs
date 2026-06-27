using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Fiscal.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class NotaFiscalConfiguration : IEntityTypeConfiguration<NotaFiscal>
{
    public void Configure(EntityTypeBuilder<NotaFiscal> b)
    {
        b.ToTable("NotasFiscais");
        b.HasKey(x => x.Id);
        b.Property(x => x.ChaveAcesso).HasMaxLength(44);
        b.Property(x => x.Protocolo).HasMaxLength(20);
        b.Property(x => x.CpfCnpjDestinatario).HasMaxLength(18);
        b.Property(x => x.NomeDestinatario).HasMaxLength(200);
        b.Property(x => x.EmailDestinatario).HasMaxLength(120);
        b.Property(x => x.TotalProdutos).HasColumnType("decimal(18,2)");
        b.Property(x => x.TotalDesconto).HasColumnType("decimal(18,2)");
        b.Property(x => x.TotalIcms).HasColumnType("decimal(18,2)");
        b.Property(x => x.TotalPis).HasColumnType("decimal(18,2)");
        b.Property(x => x.TotalCofins).HasColumnType("decimal(18,2)");
        b.Property(x => x.TotalNota).HasColumnType("decimal(18,2)");
        b.Property(x => x.XmlEnvio).HasColumnType("nvarchar(max)");
        b.Property(x => x.XmlRetorno).HasColumnType("nvarchar(max)");
        b.Property(x => x.MotivoRejeicao).HasMaxLength(1000);

        b.HasMany(x => x.Itens).WithOne().HasForeignKey(i => i.NotaFiscalId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ChaveAcesso).IsUnique().HasFilter("[ChaveAcesso] IS NOT NULL");
        b.HasIndex(x => new { x.EmpresaId, x.Modelo, x.Numero });
        b.HasIndex(x => x.DataEmissao);
    }
}

public class ItemNotaFiscalConfiguration : IEntityTypeConfiguration<ItemNotaFiscal>
{
    public void Configure(EntityTypeBuilder<ItemNotaFiscal> b)
    {
        b.ToTable("ItensNotaFiscal");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
        b.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
        b.Property(x => x.Ncm).HasMaxLength(8);
        b.Property(x => x.Cest).HasMaxLength(7);
        b.Property(x => x.Cfop).HasMaxLength(4).IsRequired();
        b.Property(x => x.UnidadeMedida).HasMaxLength(6).IsRequired();
        b.Property(x => x.Quantidade).HasColumnType("decimal(15,4)");
        b.Property(x => x.ValorUnitario).HasColumnType("decimal(18,4)");
        b.Property(x => x.ValorDesconto).HasColumnType("decimal(18,2)");
        b.Property(x => x.ValorTotal).HasColumnType("decimal(18,2)");
        b.Property(x => x.BaseIcms).HasColumnType("decimal(18,2)");
        b.Property(x => x.AliquotaIcms).HasColumnType("decimal(5,2)");
        b.Property(x => x.ValorIcms).HasColumnType("decimal(18,2)");
        b.Property(x => x.AliquotaPis).HasColumnType("decimal(5,2)");
        b.Property(x => x.ValorPis).HasColumnType("decimal(18,2)");
        b.Property(x => x.AliquotaCofins).HasColumnType("decimal(5,2)");
        b.Property(x => x.ValorCofins).HasColumnType("decimal(18,2)");
    }
}

public class ConfiguracaoFiscalConfiguration : IEntityTypeConfiguration<ConfiguracaoFiscal>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoFiscal> b)
    {
        b.ToTable("ConfiguracoesFiscais");
        b.HasKey(x => x.Id);
        b.Property(x => x.CscIdNFCe).HasMaxLength(6);
        b.Property(x => x.CscTokenNFCe).HasMaxLength(36);
        b.Property(x => x.CaminhoXmlNFe).HasMaxLength(500);
        b.Property(x => x.EmailContador).HasMaxLength(150);
        b.HasIndex(x => x.EmpresaId).IsUnique();
    }
}
