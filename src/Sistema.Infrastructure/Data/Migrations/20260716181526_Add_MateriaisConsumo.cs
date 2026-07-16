using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_MateriaisConsumo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MaterialConsumoId",
                table: "ItensEntradaNFe",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoEntrada",
                table: "EntradasNFe",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MateriaisConsumo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnidadeMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodigoFornecedor = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CodigoBarras = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EstoqueAtual = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    EstoqueMinimo = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CustoMedio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UltimoCusto = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DataUltimaCompra = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Localizacao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriaisConsumo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesMaterial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialConsumoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DocumentoOrigem = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentacoesMaterial", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MateriaisConsumo_EmpresaId_Ativo",
                table: "MateriaisConsumo",
                columns: new[] { "EmpresaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_MateriaisConsumo_EmpresaId_Codigo",
                table: "MateriaisConsumo",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesMaterial_EmpresaId_CriadoEm",
                table: "MovimentacoesMaterial",
                columns: new[] { "EmpresaId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesMaterial_MaterialConsumoId_CriadoEm",
                table: "MovimentacoesMaterial",
                columns: new[] { "MaterialConsumoId", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MateriaisConsumo");

            migrationBuilder.DropTable(
                name: "MovimentacoesMaterial");

            migrationBuilder.DropColumn(
                name: "MaterialConsumoId",
                table: "ItensEntradaNFe");

            migrationBuilder.DropColumn(
                name: "TipoEntrada",
                table: "EntradasNFe");
        }
    }
}
