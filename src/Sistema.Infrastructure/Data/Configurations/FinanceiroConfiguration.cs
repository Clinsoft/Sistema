using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Financeiro.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class CategoriaFinanceiraConfiguration : IEntityTypeConfiguration<CategoriaFinanceira>
{
    public void Configure(EntityTypeBuilder<CategoriaFinanceira> b)
    {
        b.ToTable("CategoriasFinanceiras");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).HasMaxLength(80).IsRequired();
        b.Property(x => x.Cor).HasMaxLength(10);
        b.HasIndex(x => new { x.EmpresaId, x.Tipo });
    }
}

public class ContaBancariaConfiguration : IEntityTypeConfiguration<ContaBancaria>
{
    public void Configure(EntityTypeBuilder<ContaBancaria> b)
    {
        b.ToTable("ContasBancarias");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nome).HasMaxLength(80).IsRequired();
        b.Property(x => x.Banco).HasMaxLength(60);
        b.Property(x => x.Agencia).HasMaxLength(20);
        b.Property(x => x.NumeroConta).HasMaxLength(30);
        b.Property(x => x.SaldoInicial).HasColumnType("decimal(18,2)");
        b.Property(x => x.SaldoAtual).HasColumnType("decimal(18,2)");
    }
}

public class LancamentoFinanceiroConfiguration : IEntityTypeConfiguration<LancamentoFinanceiro>
{
    public void Configure(EntityTypeBuilder<LancamentoFinanceiro> b)
    {
        b.ToTable("LancamentosFinanceiros");
        b.HasKey(x => x.Id);
        b.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
        b.Property(x => x.ValorOriginal).HasColumnType("decimal(18,2)");
        b.Property(x => x.ValorPago).HasColumnType("decimal(18,2)");
        b.Property(x => x.DocumentoOrigem).HasMaxLength(50);
        b.Property(x => x.GrupoParcelamento).HasMaxLength(36);
        b.Property(x => x.Observacao).HasMaxLength(500);
        b.HasIndex(x => new { x.EmpresaId, x.Tipo, x.Status });
        b.HasIndex(x => new { x.EmpresaId, x.DataVencimento });
        b.HasIndex(x => x.ClienteId);
        b.HasIndex(x => x.FornecedorId);

        // Propriedades calculadas — ignoradas no mapeamento
        b.Ignore(x => x.Saldo);
        b.Ignore(x => x.Vencido);
    }
}

public class MovimentacaoBancariaConfiguration : IEntityTypeConfiguration<MovimentacaoBancaria>
{
    public void Configure(EntityTypeBuilder<MovimentacaoBancaria> b)
    {
        b.ToTable("MovimentacoesBancarias");
        b.HasKey(x => x.Id);
        b.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
        b.Property(x => x.Valor).HasColumnType("decimal(18,2)");
        b.Property(x => x.SaldoApos).HasColumnType("decimal(18,2)");
        b.HasIndex(x => new { x.ContaBancariaId, x.DataMovimentacao });
    }
}

public class CustoFixoConfiguration : IEntityTypeConfiguration<CustoFixo>
{
    public void Configure(EntityTypeBuilder<CustoFixo> b)
    {
        b.ToTable("CustosFixos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Descricao).HasMaxLength(150).IsRequired();
        b.Property(x => x.Categoria).HasMaxLength(80);
        b.Property(x => x.Valor).HasColumnType("decimal(18,2)");
    }
}
