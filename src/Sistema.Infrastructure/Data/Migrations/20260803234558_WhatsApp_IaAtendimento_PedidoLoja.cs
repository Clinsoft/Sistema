using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WhatsApp_IaAtendimento_PedidoLoja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocalEstoqueId",
                table: "PedidosWhatsApp",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IaAtendimentoAtiva",
                table: "ConfiguracoesWhatsAppMensagem",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalEstoqueId",
                table: "PedidosWhatsApp");

            migrationBuilder.DropColumn(
                name: "IaAtendimentoAtiva",
                table: "ConfiguracoesWhatsAppMensagem");
        }
    }
}
