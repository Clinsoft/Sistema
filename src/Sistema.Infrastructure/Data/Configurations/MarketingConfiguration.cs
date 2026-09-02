using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Marketing.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class TemplateMarketingConfiguration : IEntityTypeConfiguration<TemplateMarketing>
{
    public void Configure(EntityTypeBuilder<TemplateMarketing> b)
    {
        b.ToTable("TemplatesMarketing");
        b.HasKey(t => t.Id);
        b.Property(t => t.Nome).HasMaxLength(150).IsRequired();
        b.Property(t => t.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(t => t.Formato).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.LayoutJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(t => t.ThumbnailBase64).HasColumnType("nvarchar(max)");
    }
}

public class ArteMarketingConfiguration : IEntityTypeConfiguration<ArteMarketing>
{
    public void Configure(EntityTypeBuilder<ArteMarketing> b)
    {
        b.ToTable("ArtesMarketing");
        b.HasKey(a => a.Id);
        b.HasIndex(a => new { a.EmpresaId, a.CriadoEm });
        b.Property(a => a.Nome).HasMaxLength(150).IsRequired();
        b.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.Formato).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(15);
        b.Property(a => a.LayoutJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(a => a.ThumbnailBase64).HasColumnType("nvarchar(max)");
        b.Property(a => a.UrlExportada).HasMaxLength(500);
    }
}

public class PromocaoConfiguration : IEntityTypeConfiguration<Promocao>
{
    public void Configure(EntityTypeBuilder<Promocao> b)
    {
        b.ToTable("Promocoes");
        b.HasKey(p => p.Id);
        b.HasIndex(p => new { p.EmpresaId, p.Ativa });
        b.Property(p => p.Nome).HasMaxLength(150).IsRequired();
        b.Property(p => p.Tipo).HasMaxLength(30).IsRequired();
        b.Property(p => p.TipoDesconto).HasMaxLength(20).IsRequired();
        b.Property(p => p.AplicaEm).HasMaxLength(20).IsRequired();
        b.Property(p => p.Desconto).HasPrecision(18, 2);
        b.Property(p => p.ValorMinimoPedido).HasPrecision(18, 2);
    }
}

public class CupomSorteioConfiguration : IEntityTypeConfiguration<CupomSorteio>
{
    public void Configure(EntityTypeBuilder<CupomSorteio> b)
    {
        b.ToTable("CuponsSorteio");
        b.HasKey(c => c.Id);
        b.HasIndex(c => new { c.PromocaoId, c.Numero });
        b.HasIndex(c => new { c.EmpresaId, c.PromocaoId });
        b.Property(c => c.NomeCliente).HasMaxLength(150).IsRequired();
        b.Property(c => c.Telefone).HasMaxLength(20);
        b.Property(c => c.ValorCompra).HasPrecision(18, 2);
    }
}

public class MembroClubeConfiguration : IEntityTypeConfiguration<MembroClube>
{
    public void Configure(EntityTypeBuilder<MembroClube> b)
    {
        b.ToTable("MembrosClube");
        b.HasKey(m => m.Id);
        b.HasIndex(m => new { m.EmpresaId, m.ClienteId });
        b.Property(m => m.Status).HasMaxLength(15).IsRequired();
        b.Property(m => m.Observacao).HasMaxLength(500);
        b.Property(m => m.SaldoCashback).HasPrecision(18, 2);
        b.Property(m => m.TotalCashback).HasPrecision(18, 2);
        b.Property(m => m.TotalCompras).HasPrecision(18, 2);
    }
}

public class MovimentoCashbackConfiguration : IEntityTypeConfiguration<MovimentoCashback>
{
    public void Configure(EntityTypeBuilder<MovimentoCashback> b)
    {
        b.ToTable("MovimentosCashback");
        b.HasKey(m => m.Id);
        b.HasIndex(m => new { m.EmpresaId, m.Data });
        b.Property(m => m.Tipo).HasMaxLength(10).IsRequired();
        b.Property(m => m.Motivo).HasMaxLength(300);
        b.Property(m => m.VendaNumero).HasMaxLength(30);
        b.Property(m => m.Valor).HasPrecision(18, 2);
        b.Property(m => m.DescontoUsado).HasPrecision(18, 2);
    }
}

public class ConfiguracaoClubeConfiguration : IEntityTypeConfiguration<ConfiguracaoClube>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoClube> b)
    {
        b.ToTable("ConfiguracoesClube");
        b.HasKey(c => c.Id);
        b.HasIndex(c => c.EmpresaId).IsUnique();
        b.Property(c => c.NomeClubeExibicao).HasMaxLength(80).IsRequired();
        b.Property(c => c.PercentualCashback).HasPrecision(9, 2);
        b.Property(c => c.MinimoResgate).HasPrecision(18, 2);
        b.Property(c => c.LimiteUsoPercent).HasPrecision(9, 2);
        b.Property(c => c.DescontoMembro).HasPrecision(9, 2);
    }
}

public class AgendamentoPublicacaoConfiguration : IEntityTypeConfiguration<AgendamentoPublicacao>
{
    public void Configure(EntityTypeBuilder<AgendamentoPublicacao> b)
    {
        b.ToTable("AgendamentosPublicacao");
        b.HasKey(a => a.Id);
        b.HasIndex(a => new { a.EmpresaId, a.DataHoraAgendada, a.Status });
        b.Property(a => a.Rede).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(15);
        b.Property(a => a.Legenda).HasMaxLength(2200);
        b.Property(a => a.ErroPublicacao).HasMaxLength(500);
    }
}
