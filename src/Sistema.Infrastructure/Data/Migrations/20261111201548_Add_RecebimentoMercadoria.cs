using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_RecebimentoMercadoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecebimentosMercadoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChaveNota = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecebidoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecebidoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecebimentosMercadoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentosMercadoria_ProdutoId_LocalEstoqueId",
                table: "RecebimentosMercadoria",
                columns: new[] { "ProdutoId", "LocalEstoqueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecebimentosMercadoria");
        }
    }
}
