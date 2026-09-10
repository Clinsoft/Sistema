using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_EntradaNFe_FreteCteAplicado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FreteCteAplicado",
                table: "EntradasNFe",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreteCteAplicado",
                table: "EntradasNFe");
        }
    }
}
