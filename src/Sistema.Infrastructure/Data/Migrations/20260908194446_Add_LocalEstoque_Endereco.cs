using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_LocalEstoque_Endereco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "LocaisEstoque",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "LocaisEstoque",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                table: "LocaisEstoque",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Complemento",
                table: "LocaisEstoque",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logradouro",
                table: "LocaisEstoque",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Numero",
                table: "LocaisEstoque",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefone",
                table: "LocaisEstoque",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uf",
                table: "LocaisEstoque",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Cidade",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Complemento",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Logradouro",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Telefone",
                table: "LocaisEstoque");

            migrationBuilder.DropColumn(
                name: "Uf",
                table: "LocaisEstoque");
        }
    }
}
