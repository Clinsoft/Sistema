using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_EntradaNFePedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntradasNFePedidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntradaNFeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PedidoCompraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasNFePedidos", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntradasNFePedidos");
        }
    }
}
