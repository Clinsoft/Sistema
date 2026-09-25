using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Assinaturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assinaturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plano = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Ciclo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TrialAte = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProximoVencimento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LojasContratadas = table.Column<int>(type: "int", nullable: false),
                    DiasTolerancia = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assinaturas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assinaturas_EmpresaId",
                table: "Assinaturas",
                column: "EmpresaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assinaturas");
        }
    }
}
