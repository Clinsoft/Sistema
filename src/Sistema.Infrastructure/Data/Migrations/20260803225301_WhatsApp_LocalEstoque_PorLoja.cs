using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WhatsApp_LocalEstoque_PorLoja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocalEstoqueId",
                table: "MensagensWhatsApp",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocalEstoqueId",
                table: "HistoricosMensagensWhatsApp",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocalEstoqueId",
                table: "ConfiguracoesWhatsAppMensagem",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalEstoqueId",
                table: "MensagensWhatsApp");

            migrationBuilder.DropColumn(
                name: "LocalEstoqueId",
                table: "HistoricosMensagensWhatsApp");

            migrationBuilder.DropColumn(
                name: "LocalEstoqueId",
                table: "ConfiguracoesWhatsAppMensagem");
        }
    }
}
