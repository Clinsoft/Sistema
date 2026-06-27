using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fiscal_NfcE_ReformaTributaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CpfConsumidor",
                table: "Vendas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NotaFiscalId",
                table: "Vendas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CpfConsumidor",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCbs",
                table: "NotasFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalIbs",
                table: "NotasFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalIs",
                table: "NotasFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSplitPayment",
                table: "NotasFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UrlConsultaQrCode",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaCbs",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaIbs",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaIs",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseCbs",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseIbs",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorCbs",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorIbs",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorIs",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorRetidoSplitPayment",
                table: "ItensNotaFiscal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CpfConsumidor",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "NotaFiscalId",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "CpfConsumidor",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TotalCbs",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TotalIbs",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TotalIs",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TotalSplitPayment",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "UrlConsultaQrCode",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "AliquotaCbs",
                table: "ItensNotaFiscal");

            migrationBuilder.DropColumn(
                name: "AliquotaIbs",
                table: "ItensNotaFiscal");

            migrationBuilder.DropColumn(
                name: "AliquotaIs",
                table: "ItensNotaFiscal");

            migrationBuilder.DropColumn(
                name: "BaseCbs",
                table: "ItensNotaFiscal");

            migrationBuilder.DropColumn(
                name: "BaseIbs",
                table: "ItensNotaFiscal");

            migrationBuilder.DropColumn(
                name: "ValorCbs",
                table: "ItensNotaFiscal");

            migrationBuilder.DropColumn(
                name: "ValorIbs",
                table: "ItensNotaFiscal");

            migrationBuilder.DropColumn(
                name: "ValorIs",
                table: "ItensNotaFiscal");

            migrationBuilder.DropColumn(
                name: "ValorRetidoSplitPayment",
                table: "ItensNotaFiscal");
        }
    }
}
