using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConcorrentesEGeoLoja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GeocodificadoEm",
                table: "LocaisEstoque",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "LocaisEstoque",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "LocaisEstoque",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Concorrentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    DistanciaKm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Endereco = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Fonte = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OsmRef = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MonitorarOnline = table.Column<bool>(type: "bit", nullable: false),
                    UrlOnline = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concorrentes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Concorrentes_EmpresaId",
                table: "Concorrentes",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Concorrentes_LocalEstoqueId_OsmRef",
                table: "Concorrentes",
                columns: new[] { "LocalEstoqueId", "OsmRef" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Concorrentes");

            migrationBuilder.DropColumn(
                name: "GeocodificadoEm",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "LocaisEstoque");
        }
    }
}
