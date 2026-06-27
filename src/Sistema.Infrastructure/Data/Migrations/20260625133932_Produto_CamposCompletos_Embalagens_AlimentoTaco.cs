using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Produto_CamposCompletos_Embalagens_AlimentoTaco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoFci",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagemUrl",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InformacaoAdicional",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Marcador",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkupAtacado",
                table: "Produtos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkupMinimo",
                table: "Produtos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "OcultarNasVendas",
                table: "Produtos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoFornecedor",
                table: "Produtos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoMinimo",
                table: "Produtos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Referencia",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequisitarVendedor",
                table: "Produtos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoVariacao",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ValidadeEmDias",
                table: "Produtos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlimentosTaco",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NomeCientifico = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GrupoAlimentar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fonte = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CaloriasKcal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CaloriasKj = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Umidade = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Carboidratos = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Proteinas = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LipidiosTotais = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FibraAlimentar = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Cinzas = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Colesterol = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AcucaresTotais = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GordurasSaturadas = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GordurasMono = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GordurasPoli = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GordurasTrans = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Sodio = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Potassio = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Calcio = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Magnesio = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Manganes = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Fosforo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Ferro = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Zinco = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Cobre = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Selenio = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VitaminaA = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VitaminaD = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VitaminaE = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VitaminaK = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VitaminaC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Tiamina = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Riboflavina = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Niacina = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VitaminaB6 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AcidoFolico = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VitaminaB12 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlimentosTaco", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProdutosEmbalagem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnidadeMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Multiplicador = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CodigoBarras = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrecoVenda = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProdutosEmbalagem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProdutosEmbalagem_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosEmbalagem_ProdutoId",
                table: "ProdutosEmbalagem",
                column: "ProdutoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlimentosTaco");

            migrationBuilder.DropTable(
                name: "ProdutosEmbalagem");

            migrationBuilder.DropColumn(
                name: "CodigoFci",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "ImagemUrl",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "InformacaoAdicional",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Marcador",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "MarkupAtacado",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "MarkupMinimo",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "OcultarNasVendas",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "PrecoFornecedor",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "PrecoMinimo",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Referencia",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "RequisitarVendedor",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "TipoVariacao",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "ValidadeEmDias",
                table: "Produtos");
        }
    }
}
