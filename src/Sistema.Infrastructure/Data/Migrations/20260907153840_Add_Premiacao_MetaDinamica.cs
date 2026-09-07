using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Premiacao_MetaDinamica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetasPremiacaoLoja_EmpresaId_LocalEstoqueId",
                table: "MetasPremiacaoLoja");

            migrationBuilder.AddColumn<int>(
                name: "Ano",
                table: "MetasPremiacaoLoja",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Mes",
                table: "MetasPremiacaoLoja",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FatorMetaLoja",
                table: "ConfiguracoesPremiacao",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MesesBaseMeta",
                table: "ConfiguracoesPremiacao",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MetasPremiacaoLoja_EmpresaId_LocalEstoqueId_Ano_Mes",
                table: "MetasPremiacaoLoja",
                columns: new[] { "EmpresaId", "LocalEstoqueId", "Ano", "Mes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetasPremiacaoLoja_EmpresaId_LocalEstoqueId_Ano_Mes",
                table: "MetasPremiacaoLoja");

            migrationBuilder.DropColumn(
                name: "Ano",
                table: "MetasPremiacaoLoja");

            migrationBuilder.DropColumn(
                name: "Mes",
                table: "MetasPremiacaoLoja");

            migrationBuilder.DropColumn(
                name: "FatorMetaLoja",
                table: "ConfiguracoesPremiacao");

            migrationBuilder.DropColumn(
                name: "MesesBaseMeta",
                table: "ConfiguracoesPremiacao");

            migrationBuilder.CreateIndex(
                name: "IX_MetasPremiacaoLoja_EmpresaId_LocalEstoqueId",
                table: "MetasPremiacaoLoja",
                columns: new[] { "EmpresaId", "LocalEstoqueId" },
                unique: true);
        }
    }
}
