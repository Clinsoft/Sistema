using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fiscal_NotasFiscais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesFiscais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Regime = table.Column<int>(type: "int", nullable: false),
                    Ambiente = table.Column<int>(type: "int", nullable: false),
                    SerieNFe = table.Column<int>(type: "int", nullable: false),
                    SerieNFCe = table.Column<int>(type: "int", nullable: false),
                    ProximoNumerNFe = table.Column<long>(type: "bigint", nullable: false),
                    ProximoNumerNFCe = table.Column<long>(type: "bigint", nullable: false),
                    CscIdNFCe = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    CscTokenNFCe = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    CaminhoXmlNFe = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmailContador = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EnviarEmailAposEmissao = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesFiscais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotasFiscais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Modelo = table.Column<int>(type: "int", nullable: false),
                    Serie = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<long>(type: "bigint", nullable: false),
                    ChaveAcesso = table.Column<string>(type: "nvarchar(44)", maxLength: 44, nullable: true),
                    Protocolo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NaturezaOperacao = table.Column<int>(type: "int", nullable: false),
                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataSaida = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CpfCnpjDestinatario = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: true),
                    NomeDestinatario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmailDestinatario = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TotalProdutos = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDesconto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalIcms = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPis = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCofins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalNota = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    XmlEnvio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    XmlRetorno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MotivoRejeicao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChaveCartaCorrecao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensNotaFiscal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaFiscalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NumeroItem = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ncm = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Cest = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Cfop = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    UnidadeMedida = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(15,4)", nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CstIcms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CsosnIcms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaseIcms = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AliquotaIcms = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ValorIcms = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CstPisCofins = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AliquotaPis = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ValorPis = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AliquotaCofins = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ValorCofins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensNotaFiscal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensNotaFiscal_NotasFiscais_NotaFiscalId",
                        column: x => x.NotaFiscalId,
                        principalTable: "NotasFiscais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesFiscais_EmpresaId",
                table: "ConfiguracoesFiscais",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensNotaFiscal_NotaFiscalId",
                table: "ItensNotaFiscal",
                column: "NotaFiscalId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_ChaveAcesso",
                table: "NotasFiscais",
                column: "ChaveAcesso",
                unique: true,
                filter: "[ChaveAcesso] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_DataEmissao",
                table: "NotasFiscais",
                column: "DataEmissao");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_EmpresaId_Modelo_Numero",
                table: "NotasFiscais",
                columns: new[] { "EmpresaId", "Modelo", "Numero" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesFiscais");

            migrationBuilder.DropTable(
                name: "ItensNotaFiscal");

            migrationBuilder.DropTable(
                name: "NotasFiscais");
        }
    }
}
