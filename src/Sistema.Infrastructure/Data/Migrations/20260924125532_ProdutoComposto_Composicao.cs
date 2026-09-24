using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProdutoComposto_Composicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EhComposto",
                table: "Produtos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RendimentoComposicao",
                table: "Produtos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ComponentesComposicao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoCompostoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponenteProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unidade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentesComposicao", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesComposicao_ProdutoCompostoId",
                table: "ComponentesComposicao",
                column: "ProdutoCompostoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComponentesComposicao");

            migrationBuilder.DropColumn(
                name: "EhComposto",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "RendimentoComposicao",
                table: "Produtos");
        }
    }
}
