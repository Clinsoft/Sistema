using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Produto_CodigoFornecedorPrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoFornecedorPrincipal",
                table: "Produtos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_EmpresaId_FornecedorPrincipalId_CodigoFornecedorPrincipal",
                table: "Produtos",
                columns: new[] { "EmpresaId", "FornecedorPrincipalId", "CodigoFornecedorPrincipal" },
                filter: "[CodigoFornecedorPrincipal] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Produtos_EmpresaId_FornecedorPrincipalId_CodigoFornecedorPrincipal",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "CodigoFornecedorPrincipal",
                table: "Produtos");
        }
    }
}
