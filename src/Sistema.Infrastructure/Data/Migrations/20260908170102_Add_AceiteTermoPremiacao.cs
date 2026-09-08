using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_AceiteTermoPremiacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AceitesTermoPremiacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColaboradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColaboradorNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TermoVersao = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TermoHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DataAceite = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FotoBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssinaturaBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    PrecisaoMetros = table.Column<double>(type: "float", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AceitesTermoPremiacao", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AceitesTermoPremiacao_EmpresaId_ColaboradorId",
                table: "AceitesTermoPremiacao",
                columns: new[] { "EmpresaId", "ColaboradorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AceitesTermoPremiacao");
        }
    }
}
