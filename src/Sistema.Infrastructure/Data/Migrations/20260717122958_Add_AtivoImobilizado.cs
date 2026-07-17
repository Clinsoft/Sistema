using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_AtivoImobilizado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AtivoImobilizadoId",
                table: "ItensEntradaNFe",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AtivosImobilizados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FornecedorPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NotaFiscal = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    NumeroSerie = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Localizacao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DataAquisicao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValorAquisicao = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    VidaUtilMeses = table.Column<int>(type: "int", nullable: false),
                    ValorResidual = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataBaixa = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoBaixa = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtivosImobilizados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AtivosImobilizados_EmpresaId_Ativo",
                table: "AtivosImobilizados",
                columns: new[] { "EmpresaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_AtivosImobilizados_EmpresaId_Codigo",
                table: "AtivosImobilizados",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtivosImobilizados");

            migrationBuilder.DropColumn(
                name: "AtivoImobilizadoId",
                table: "ItensEntradaNFe");
        }
    }
}
