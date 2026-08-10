using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Cliente_Loja_Financiamentos_ValorJuros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ValorJuros",
                table: "LancamentosFinanceiros",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocalEstoqueId",
                table: "Clientes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Financiamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Banco = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValorCredito = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorParcela = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NumeroParcelas = table.Column<int>(type: "int", nullable: false),
                    TaxaEfetivaMensal = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    TaxaNominalMensal = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    PrimeiroVencimento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrupoParcelamento = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    ContratoPdfUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LancouEntrada = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Financiamentos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Financiamentos_EmpresaId",
                table: "Financiamentos",
                column: "EmpresaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Financiamentos");

            migrationBuilder.DropColumn(
                name: "ValorJuros",
                table: "LancamentosFinanceiros");

            migrationBuilder.DropColumn(
                name: "LocalEstoqueId",
                table: "Clientes");
        }
    }
}
