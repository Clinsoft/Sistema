using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_PagamentoVenda_OperadoraCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperadoraCartaoId",
                table: "PagamentosVenda",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosVenda_OperadoraCartaoId",
                table: "PagamentosVenda",
                column: "OperadoraCartaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PagamentosVenda_OperadoraCartaoId",
                table: "PagamentosVenda");

            migrationBuilder.DropColumn(
                name: "OperadoraCartaoId",
                table: "PagamentosVenda");
        }
    }
}
