using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_AuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Acao = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Entidade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntidadeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Resumo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Alteracoes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EmpresaId_DataHora",
                table: "AuditLogs",
                columns: new[] { "EmpresaId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entidade_EntidadeId",
                table: "AuditLogs",
                columns: new[] { "Entidade", "EntidadeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UsuarioId",
                table: "AuditLogs",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
