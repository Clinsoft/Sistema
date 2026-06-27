using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Contabilidade_PlanoContas_Lancamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LancamentosContabeis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataLancamento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataCompetencia = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Historico = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentoOrigem = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Estornado = table.Column<bool>(type: "bit", nullable: false),
                    LancamentoEstornoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LancamentosContabeis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanoContas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Natureza = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    ContaPaiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    AceitaLancamento = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanoContas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartidasContabeis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LancamentoContabilId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContaContabilId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Natureza = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Complemento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartidasContabeis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartidasContabeis_LancamentosContabeis_LancamentoContabilId",
                        column: x => x.LancamentoContabilId,
                        principalTable: "LancamentosContabeis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LancamentosContabeis_EmpresaId_DataCompetencia",
                table: "LancamentosContabeis",
                columns: new[] { "EmpresaId", "DataCompetencia" });

            migrationBuilder.CreateIndex(
                name: "IX_LancamentosContabeis_EmpresaId_Numero",
                table: "LancamentosContabeis",
                columns: new[] { "EmpresaId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_PartidasContabeis_LancamentoContabilId",
                table: "PartidasContabeis",
                column: "LancamentoContabilId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanoContas_EmpresaId_Codigo",
                table: "PlanoContas",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartidasContabeis");

            migrationBuilder.DropTable(
                name: "PlanoContas");

            migrationBuilder.DropTable(
                name: "LancamentosContabeis");
        }
    }
}
