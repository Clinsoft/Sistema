using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_ConfigWhats_ResumoDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnviarResumoDiario",
                table: "ConfiguracoesWhatsAppMensagem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TelefoneResumoDiario",
                table: "ConfiguracoesWhatsAppMensagem",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnviarResumoDiario",
                table: "ConfiguracoesWhatsAppMensagem");

            migrationBuilder.DropColumn(
                name: "TelefoneResumoDiario",
                table: "ConfiguracoesWhatsAppMensagem");
        }
    }
}
