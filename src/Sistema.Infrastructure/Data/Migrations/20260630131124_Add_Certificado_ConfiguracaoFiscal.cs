using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Certificado_ConfiguracaoFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificadoPfxBase64",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificadoSenha",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperadorasCartao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Icone = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Bandeiras = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TaxaDebito = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    TaxaCreditoVista = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    TaxaCreditoParcelado = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    TaxaPix = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    TaxaAntecipacao = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    PrazoDiasDebito = table.Column<int>(type: "int", nullable: false),
                    PrazoDiasCreditoVista = table.Column<int>(type: "int", nullable: false),
                    PrazoDiasCreditoParcelado = table.Column<int>(type: "int", nullable: false),
                    PrazoDiasPix = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperadorasCartao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiveisCartao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperadoraCartaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FormaPagamento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Parcelas = table.Column<int>(type: "int", nullable: false),
                    ValorBruto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Taxa = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    ValorLiquido = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataTransacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataPrevistaRepasse = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataRepasse = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataAntecipacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaxaAntecipacaoAplicada = table.Column<decimal>(type: "decimal(6,4)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NsuTid = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiveisCartao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiveisCartao_OperadorasCartao_OperadoraCartaoId",
                        column: x => x.OperadoraCartaoId,
                        principalTable: "OperadorasCartao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperadorasCartao_EmpresaId_Ativo",
                table: "OperadorasCartao",
                columns: new[] { "EmpresaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveisCartao_EmpresaId_Status_DataPrevistaRepasse",
                table: "ReceiveisCartao",
                columns: new[] { "EmpresaId", "Status", "DataPrevistaRepasse" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveisCartao_OperadoraCartaoId",
                table: "ReceiveisCartao",
                column: "OperadoraCartaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiveisCartao");

            migrationBuilder.DropTable(
                name: "OperadorasCartao");

            migrationBuilder.DropColumn(
                name: "CertificadoPfxBase64",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CertificadoSenha",
                table: "ConfiguracoesFiscais");
        }
    }
}
