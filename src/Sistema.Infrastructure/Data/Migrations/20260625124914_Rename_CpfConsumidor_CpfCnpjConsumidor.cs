using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Rename_CpfConsumidor_CpfCnpjConsumidor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CpfConsumidor",
                table: "Vendas",
                newName: "CpfCnpjConsumidor");

            migrationBuilder.RenameColumn(
                name: "CpfConsumidor",
                table: "NotasFiscais",
                newName: "CpfCnpjConsumidor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CpfCnpjConsumidor",
                table: "Vendas",
                newName: "CpfConsumidor");

            migrationBuilder.RenameColumn(
                name: "CpfCnpjConsumidor",
                table: "NotasFiscais",
                newName: "CpfConsumidor");
        }
    }
}
