using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_MetaVendaMensal_LocalEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetasVendaMensal_EmpresaId_Ano_Mes",
                table: "MetasVendaMensal");

            migrationBuilder.AddColumn<Guid>(
                name: "LocalEstoqueId",
                table: "MetasVendaMensal",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetasVendaMensal_EmpresaId_LocalEstoqueId_Ano_Mes",
                table: "MetasVendaMensal",
                columns: new[] { "EmpresaId", "LocalEstoqueId", "Ano", "Mes" },
                unique: true,
                filter: "[LocalEstoqueId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetasVendaMensal_EmpresaId_LocalEstoqueId_Ano_Mes",
                table: "MetasVendaMensal");

            migrationBuilder.DropColumn(
                name: "LocalEstoqueId",
                table: "MetasVendaMensal");

            migrationBuilder.CreateIndex(
                name: "IX_MetasVendaMensal_EmpresaId_Ano_Mes",
                table: "MetasVendaMensal",
                columns: new[] { "EmpresaId", "Ano", "Mes" },
                unique: true);
        }
    }
}
