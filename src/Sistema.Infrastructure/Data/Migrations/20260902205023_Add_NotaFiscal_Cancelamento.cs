using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_NotaFiscal_Cancelamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataCancelamento",
                table: "NotasFiscais",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JustificativaCancelamento",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtocoloCancelamento",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataCancelamento",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "JustificativaCancelamento",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "ProtocoloCancelamento",
                table: "NotasFiscais");
        }
    }
}
