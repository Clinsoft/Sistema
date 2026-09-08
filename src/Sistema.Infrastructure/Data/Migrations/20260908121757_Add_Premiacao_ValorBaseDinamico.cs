using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Premiacao_ValorBaseDinamico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PercentFaturamentoPremio",
                table: "ConfiguracoesPremiacao",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ValorBaseDinamico",
                table: "ConfiguracoesPremiacao",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PercentFaturamentoPremio",
                table: "ConfiguracoesPremiacao");

            migrationBuilder.DropColumn(
                name: "ValorBaseDinamico",
                table: "ConfiguracoesPremiacao");
        }
    }
}
