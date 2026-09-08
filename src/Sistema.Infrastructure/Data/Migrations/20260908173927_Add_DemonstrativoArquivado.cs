using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_DemonstrativoArquivado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemonstrativosArquivados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColaboradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColaboradorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    Premio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    GeradoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemonstrativosArquivados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemonstrativosArquivados_EmpresaId_Ano_Mes",
                table: "DemonstrativosArquivados",
                columns: new[] { "EmpresaId", "Ano", "Mes" });

            migrationBuilder.CreateIndex(
                name: "IX_DemonstrativosArquivados_EmpresaId_ColaboradorId_Ano_Mes",
                table: "DemonstrativosArquivados",
                columns: new[] { "EmpresaId", "ColaboradorId", "Ano", "Mes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemonstrativosArquivados");
        }
    }
}
