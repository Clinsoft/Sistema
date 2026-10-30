using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_EtiquetaImpressaLoja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PrecoAtualizadoEm",
                table: "Produtos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EtiquetasImpressasLoja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpressaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtiquetasImpressasLoja", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EtiquetasImpressasLoja_ProdutoId_LocalEstoqueId",
                table: "EtiquetasImpressasLoja",
                columns: new[] { "ProdutoId", "LocalEstoqueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EtiquetasImpressasLoja");

            migrationBuilder.DropColumn(
                name: "PrecoAtualizadoEm",
                table: "Produtos");
        }
    }
}
