using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Cadastros.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class FornecedorConfiguration : IEntityTypeConfiguration<Fornecedor>
{
    public void Configure(EntityTypeBuilder<Fornecedor> b)
    {
        b.ToTable("Fornecedores");
        b.HasKey(f => f.Id);
        b.Property(f => f.RazaoSocial).HasMaxLength(150).IsRequired();
        b.Property(f => f.NomeFantasia).HasMaxLength(150);
        b.Property(f => f.Cnpj).HasMaxLength(14);
        b.HasIndex(f => new { f.EmpresaId, f.Cnpj }).IsUnique().HasFilter("[Cnpj] IS NOT NULL");
        b.Property(f => f.InscricaoEstadual).HasMaxLength(30);
        b.Property(f => f.Email).HasMaxLength(150);
        b.Property(f => f.Telefone).HasMaxLength(20);
        b.Property(f => f.Celular).HasMaxLength(20);
        b.Property(f => f.Tipos).HasMaxLength(200);
        b.Property(f => f.Contato).HasMaxLength(100);
        b.Property(f => f.Logradouro).HasMaxLength(100);
        b.Property(f => f.Numero).HasMaxLength(10);
        b.Property(f => f.Complemento).HasMaxLength(50);
        b.Property(f => f.Bairro).HasMaxLength(60);
        b.Property(f => f.Cidade).HasMaxLength(60);
        b.Property(f => f.Uf).HasMaxLength(2);
        b.Property(f => f.Cep).HasMaxLength(8);
        b.Property(f => f.Observacao).HasMaxLength(500);
    }
}
