using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Cadastros.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Clientes");
        b.HasKey(c => c.Id);
        b.Property(c => c.Nome).HasMaxLength(150).IsRequired();
        b.Property(c => c.CpfCnpj).HasMaxLength(14);
        b.HasIndex(c => new { c.EmpresaId, c.CpfCnpj }).HasFilter("[CpfCnpj] IS NOT NULL");
        b.Property(c => c.Email).HasMaxLength(150);
        b.Property(c => c.Telefone).HasMaxLength(20);
        b.Property(c => c.Celular).HasMaxLength(20);
        b.Property(c => c.Logradouro).HasMaxLength(100);
        b.Property(c => c.Numero).HasMaxLength(10);
        b.Property(c => c.Complemento).HasMaxLength(50);
        b.Property(c => c.Bairro).HasMaxLength(60);
        b.Property(c => c.Cidade).HasMaxLength(60);
        b.Property(c => c.Uf).HasMaxLength(2);
        b.Property(c => c.Cep).HasMaxLength(8);
        b.Property(c => c.Classificacao).HasMaxLength(50);
        b.Property(c => c.Observacao).HasMaxLength(500);
        b.Property(c => c.LimiteCredito).HasColumnType("decimal(18,2)");
        b.Property(c => c.TipoPessoa).HasConversion<string>().HasMaxLength(10);
    }
}
