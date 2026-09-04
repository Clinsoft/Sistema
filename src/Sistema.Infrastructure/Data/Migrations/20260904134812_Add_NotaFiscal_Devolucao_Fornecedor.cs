using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_NotaFiscal_Devolucao_Fornecedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BairroDest",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CepDest",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChaveReferenciada",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodMunicipioDest",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntradaNFeId",
                table: "NotasFiscais",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Finalidade",
                table: "NotasFiscais",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IeDestinatario",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogradouroDest",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipioDest",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDest",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UfDest",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfopDevolucaoDentroUF",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfopDevolucaoForaUF",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BairroDest",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "CepDest",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "ChaveReferenciada",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "CodMunicipioDest",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "EntradaNFeId",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "Finalidade",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "IeDestinatario",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "LogradouroDest",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "MunicipioDest",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "NumeroDest",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "UfDest",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "CfopDevolucaoDentroUF",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CfopDevolucaoForaUF",
                table: "ConfiguracoesFiscais");
        }
    }
}
