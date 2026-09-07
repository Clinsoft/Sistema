using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Premiacao_Desempenho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApuracoesPremiacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColaboradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    PresencaPercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    FaltaInjustificada = table.Column<bool>(type: "bit", nullable: false),
                    Advertencia = table.Column<bool>(type: "bit", nullable: false),
                    ExecucaoMinima = table.Column<bool>(type: "bit", nullable: false),
                    ProdutoVencidoExposto = table.Column<bool>(type: "bit", nullable: false),
                    HigieneGrave = table.Column<bool>(type: "bit", nullable: false),
                    RotinaNaoExecutada = table.Column<bool>(type: "bit", nullable: false),
                    ReclamacaoRelevante = table.Column<bool>(type: "bit", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApuracoesPremiacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvaliacoesDesempenho",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColaboradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioSemana = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    Abordagem = table.Column<int>(type: "int", nullable: false),
                    Diagnostico = table.Column<int>(type: "int", nullable: false),
                    ConexaoProduto = table.Column<int>(type: "int", nullable: false),
                    SugestaoComplementar = table.Column<int>(type: "int", nullable: false),
                    Fechamento = table.Column<int>(type: "int", nullable: false),
                    Abastecimento = table.Column<int>(type: "int", nullable: false),
                    Organizacao = table.Column<int>(type: "int", nullable: false),
                    Rotina = table.Column<int>(type: "int", nullable: false),
                    Validade = table.Column<int>(type: "int", nullable: false),
                    Perdas = table.Column<int>(type: "int", nullable: false),
                    Armazenamento = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacoesDesempenho", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesPremiacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValorBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RedutorPercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    MinPresenca = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    ThresholdLoja = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    ThresholdIndividual = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesPremiacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetasPremiacaoLoja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaLoja = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MetaIndividual = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasPremiacaoLoja", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApuracoesPremiacao_ColaboradorId_Ano_Mes",
                table: "ApuracoesPremiacao",
                columns: new[] { "ColaboradorId", "Ano", "Mes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesDesempenho_ColaboradorId_InicioSemana",
                table: "AvaliacoesDesempenho",
                columns: new[] { "ColaboradorId", "InicioSemana" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesDesempenho_EmpresaId_Ano_Mes",
                table: "AvaliacoesDesempenho",
                columns: new[] { "EmpresaId", "Ano", "Mes" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesPremiacao_EmpresaId",
                table: "ConfiguracoesPremiacao",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetasPremiacaoLoja_EmpresaId_LocalEstoqueId",
                table: "MetasPremiacaoLoja",
                columns: new[] { "EmpresaId", "LocalEstoqueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApuracoesPremiacao");

            migrationBuilder.DropTable(
                name: "AvaliacoesDesempenho");

            migrationBuilder.DropTable(
                name: "ConfiguracoesPremiacao");

            migrationBuilder.DropTable(
                name: "MetasPremiacaoLoja");
        }
    }
}
