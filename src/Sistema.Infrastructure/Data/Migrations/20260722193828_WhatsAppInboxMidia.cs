using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppInboxMidia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MidiaMime",
                table: "MensagensWhatsApp",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MidiaNome",
                table: "MensagensWhatsApp",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MidiaUrl",
                table: "MensagensWhatsApp",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusEntrega",
                table: "MensagensWhatsApp",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MidiaMime",
                table: "MensagensWhatsApp");

            migrationBuilder.DropColumn(
                name: "MidiaNome",
                table: "MensagensWhatsApp");

            migrationBuilder.DropColumn(
                name: "MidiaUrl",
                table: "MensagensWhatsApp");

            migrationBuilder.DropColumn(
                name: "StatusEntrega",
                table: "MensagensWhatsApp");
        }
    }
}
