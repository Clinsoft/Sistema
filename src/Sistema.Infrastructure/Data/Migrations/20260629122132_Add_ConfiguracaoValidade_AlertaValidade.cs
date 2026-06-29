using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_ConfiguracaoValidade_AlertaValidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertasValidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataValidade = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Nivel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromoGerada = table.Column<bool>(type: "bit", nullable: false),
                    ArteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertasValidade", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesValidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiasAlertaAmarelo = table.Column<int>(type: "int", nullable: false),
                    DiasAlertaVermelho = table.Column<int>(type: "int", nullable: false),
                    DiasAlertaUrgente = table.Column<int>(type: "int", nullable: false),
                    PromoAutomatica = table.Column<bool>(type: "bit", nullable: false),
                    ExigeAprovacao = table.Column<bool>(type: "bit", nullable: false),
                    DescontoAutoPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BloqueioVendaVencido = table.Column<bool>(type: "bit", nullable: false),
                    CategoriasJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesValidade", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertasValidade");

            migrationBuilder.DropTable(
                name: "ConfiguracoesValidade");
        }
    }
}
