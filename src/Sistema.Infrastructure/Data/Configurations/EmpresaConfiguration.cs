using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sistema.Domain.Cadastros.Entities;

namespace Sistema.Infrastructure.Data.Configurations;

public class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> b)
    {
        b.ToTable("Empresas");
        b.HasKey(e => e.Id);
        b.Property(e => e.RazaoSocial).HasMaxLength(150).IsRequired();
        b.Property(e => e.NomeFantasia).HasMaxLength(150).IsRequired();
        b.Property(e => e.Cnpj).HasMaxLength(14).IsRequired();
        b.HasIndex(e => e.Cnpj).IsUnique();
        b.Property(e => e.InscricaoEstadual).HasMaxLength(30);
        b.Property(e => e.InscricaoMunicipal).HasMaxLength(30);
        b.Property(e => e.RegimeTributario).HasMaxLength(2).IsRequired();
        b.Property(e => e.Logradouro).HasMaxLength(100);
        b.Property(e => e.Numero).HasMaxLength(10);
        b.Property(e => e.Complemento).HasMaxLength(50);
        b.Property(e => e.Bairro).HasMaxLength(60);
        b.Property(e => e.Cidade).HasMaxLength(60);
        b.Property(e => e.Uf).HasMaxLength(2);
        b.Property(e => e.Cep).HasMaxLength(8);
        b.Property(e => e.Telefone).HasMaxLength(20);
        b.Property(e => e.Email).HasMaxLength(100);
    }
}
