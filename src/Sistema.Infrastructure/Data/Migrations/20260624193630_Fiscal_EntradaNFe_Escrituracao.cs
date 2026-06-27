using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fiscal_EntradaNFe_Escrituracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntradasNFe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaFiscalRecebidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChaveAcesso = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmitenteNome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmitenteCnpj = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PedidoCompraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataEntrada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NaturezaOperacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorProdutos = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorFrete = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorFreteManual = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorSeguro = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorIpi = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorIcmsSt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataProcessamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataEstorno = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoEstorno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasNFe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensEntradaNFe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntradaNFeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroItem = table.Column<int>(type: "int", nullable: false),
                    CfopXml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CfopUtilizado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NcmXml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescricaoXml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigoFornecedor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CodigoBarras = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantidadeXml = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnidadeXml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValorUnitarioXml = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorTotalXml = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorIpi = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorIcmsSt = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorFreteProporcional = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProdutoDescricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FatorConversao = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnidadeEstoque = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustoUnitarioFinal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NumeroLote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Validade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstoqueMovimentado = table.Column<bool>(type: "bit", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrecoVendaSugerido = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MarkupSugerido = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensEntradaNFe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensEntradaNFe_EntradasNFe_EntradaNFeId",
                        column: x => x.EntradaNFeId,
                        principalTable: "EntradasNFe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensEntradaNFe_EntradaNFeId",
                table: "ItensEntradaNFe",
                column: "EntradaNFeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensEntradaNFe");

            migrationBuilder.DropTable(
                name: "EntradasNFe");
        }
    }
}
