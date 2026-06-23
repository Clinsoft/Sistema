using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Estoque.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class TabelaNutricionalConfiguration : IEntityTypeConfiguration<TabelaNutricional>
{
    public void Configure(EntityTypeBuilder<TabelaNutricional> b)
    {
        b.ToTable("TabelasNutricionais");
        b.HasKey(t => t.Id);
        b.HasIndex(t => t.ProdutoId).IsUnique();
        b.Property(t => t.Porcao).HasMaxLength(100).IsRequired();
        b.Property(t => t.Ingredientes).HasMaxLength(2000);
        b.Property(t => t.Alergenicos).HasMaxLength(500);
        b.Property(t => t.ModoConservacao).HasMaxLength(300);
        b.Property(t => t.InformacoesAdicionais).HasMaxLength(1000);

        foreach (var nutriente in new[]
        {
            nameof(TabelaNutricional.Calorias), nameof(TabelaNutricional.CaloriasGordura),
            nameof(TabelaNutricional.GordurasTotais), nameof(TabelaNutricional.GordurasSaturadas),
            nameof(TabelaNutricional.GordurasTrans), nameof(TabelaNutricional.Colesterol),
            nameof(TabelaNutricional.Sodio), nameof(TabelaNutricional.CarboidratosTotais),
            nameof(TabelaNutricional.FibrasDieteticas), nameof(TabelaNutricional.Acucares),
            nameof(TabelaNutricional.Proteinas), nameof(TabelaNutricional.VitaminaA),
            nameof(TabelaNutricional.VitaminaC), nameof(TabelaNutricional.VitaminaD),
            nameof(TabelaNutricional.VitaminaE), nameof(TabelaNutricional.VitaminaK),
            nameof(TabelaNutricional.VitaminaB1), nameof(TabelaNutricional.VitaminaB2),
            nameof(TabelaNutricional.VitaminaB3), nameof(TabelaNutricional.VitaminaB6),
            nameof(TabelaNutricional.VitaminaB12), nameof(TabelaNutricional.AcidoFolico),
            nameof(TabelaNutricional.Calcio), nameof(TabelaNutricional.Ferro),
            nameof(TabelaNutricional.Magnesio), nameof(TabelaNutricional.Zinco),
            nameof(TabelaNutricional.Selenio)
        })
        {
            b.Property(nutriente).HasColumnType("decimal(8,2)");
        }
    }
}

public class ReceitaProdutoConfiguration : IEntityTypeConfiguration<ReceitaProduto>
{
    public void Configure(EntityTypeBuilder<ReceitaProduto> b)
    {
        b.ToTable("ReceitasProduto");
        b.HasKey(r => r.Id);
        b.HasIndex(r => new { r.ProdutoId, r.Ordem });
        b.Property(r => r.Titulo).HasMaxLength(150).IsRequired();
        b.Property(r => r.Descricao).HasMaxLength(500);
        b.Property(r => r.Ingredientes).HasMaxLength(2000).IsRequired();
        b.Property(r => r.ModoPreparo).HasMaxLength(3000).IsRequired();
        b.Property(r => r.Dicas).HasMaxLength(500);
        b.Property(r => r.UrlFoto).HasMaxLength(500);
    }
}

public class SugestaoProdutoConfiguration : IEntityTypeConfiguration<SugestaoProduto>
{
    public void Configure(EntityTypeBuilder<SugestaoProduto> b)
    {
        b.ToTable("SugestoesProduto");
        b.HasKey(s => s.Id);
        b.HasIndex(s => new { s.ProdutoId, s.Ordem });
        b.Property(s => s.Titulo).HasMaxLength(150).IsRequired();
        b.Property(s => s.Descricao).HasMaxLength(1000).IsRequired();
        b.Property(s => s.Tipo).HasConversion<string>().HasMaxLength(25);
    }
}

public class QrCodeProdutoConfiguration : IEntityTypeConfiguration<QrCodeProduto>
{
    public void Configure(EntityTypeBuilder<QrCodeProduto> b)
    {
        b.ToTable("QrCodesProduto");
        b.HasKey(q => q.Id);
        b.HasIndex(q => q.ProdutoId).IsUnique();
        b.HasIndex(q => q.Slug).IsUnique();
        b.Property(q => q.Slug).HasMaxLength(200).IsRequired();
        b.Property(q => q.UrlPublica).HasMaxLength(500).IsRequired();
        b.Property(q => q.QrCodeBase64).HasColumnType("nvarchar(max)");
    }
}
