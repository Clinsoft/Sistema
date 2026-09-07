using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Desempenho.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class ConfiguracaoPremiacaoConfiguration : IEntityTypeConfiguration<ConfiguracaoPremiacao>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoPremiacao> b)
    {
        b.ToTable("ConfiguracoesPremiacao");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.EmpresaId).IsUnique();
        b.Property(x => x.ValorBase).HasPrecision(18, 2);
        b.Property(x => x.RedutorPercent).HasPrecision(9, 2);
        b.Property(x => x.MinPresenca).HasPrecision(9, 2);
        b.Property(x => x.ThresholdLoja).HasPrecision(9, 2);
        b.Property(x => x.ThresholdIndividual).HasPrecision(9, 2);
        b.Property(x => x.FatorMetaLoja).HasPrecision(9, 2);
    }
}

public class MetaPremiacaoLojaConfiguration : IEntityTypeConfiguration<MetaPremiacaoLoja>
{
    public void Configure(EntityTypeBuilder<MetaPremiacaoLoja> b)
    {
        b.ToTable("MetasPremiacaoLoja");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.EmpresaId, x.LocalEstoqueId, x.Ano, x.Mes }).IsUnique();
        b.Property(x => x.MetaLoja).HasPrecision(18, 2);
        b.Property(x => x.MetaIndividual).HasPrecision(18, 2);
    }
}

public class AvaliacaoDesempenhoSemanalConfiguration : IEntityTypeConfiguration<AvaliacaoDesempenhoSemanal>
{
    public void Configure(EntityTypeBuilder<AvaliacaoDesempenhoSemanal> b)
    {
        b.ToTable("AvaliacoesDesempenho");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ColaboradorId, x.InicioSemana }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.Ano, x.Mes });
        b.Property(x => x.Observacao).HasMaxLength(500);
        // enums NivelItem gravados como int (0/50/100)
        foreach (var p in new[] { nameof(AvaliacaoDesempenhoSemanal.Abordagem), nameof(AvaliacaoDesempenhoSemanal.Diagnostico),
            nameof(AvaliacaoDesempenhoSemanal.ConexaoProduto), nameof(AvaliacaoDesempenhoSemanal.SugestaoComplementar),
            nameof(AvaliacaoDesempenhoSemanal.Fechamento), nameof(AvaliacaoDesempenhoSemanal.Abastecimento),
            nameof(AvaliacaoDesempenhoSemanal.Organizacao), nameof(AvaliacaoDesempenhoSemanal.Rotina),
            nameof(AvaliacaoDesempenhoSemanal.Validade), nameof(AvaliacaoDesempenhoSemanal.Perdas),
            nameof(AvaliacaoDesempenhoSemanal.Armazenamento) })
            b.Property(p).HasConversion<int>();
    }
}

public class ApuracaoMensalPremiacaoConfiguration : IEntityTypeConfiguration<ApuracaoMensalPremiacao>
{
    public void Configure(EntityTypeBuilder<ApuracaoMensalPremiacao> b)
    {
        b.ToTable("ApuracoesPremiacao");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ColaboradorId, x.Ano, x.Mes }).IsUnique();
        b.Property(x => x.PresencaPercent).HasPrecision(9, 2);
        b.Property(x => x.Observacao).HasMaxLength(500);
    }
}
