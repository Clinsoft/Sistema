using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_ConfigWhats_BoasVindas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnviarBoasVindas",
                table: "ConfiguracoesWhatsAppMensagem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MensagemBoasVindas",
                table: "ConfiguracoesWhatsAppMensagem",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnviarBoasVindas",
                table: "ConfiguracoesWhatsAppMensagem");

            migrationBuilder.DropColumn(
                name: "MensagemBoasVindas",
                table: "ConfiguracoesWhatsAppMensagem");
        }
    }
}
