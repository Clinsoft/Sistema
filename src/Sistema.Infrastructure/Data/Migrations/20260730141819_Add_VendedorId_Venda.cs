using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_VendedorId_Venda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As demais colunas geradas no diff (drift) já existem no banco por ALTERs
            // manuais anteriores — esta migração adiciona apenas VendedorId.
            migrationBuilder.AddColumn<Guid>(
                name: "VendedorId",
                table: "Vendas",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VendedorId",
                table: "Vendas");
        }
    }
}
