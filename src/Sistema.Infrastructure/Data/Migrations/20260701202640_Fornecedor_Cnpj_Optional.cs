using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fornecedor_Cnpj_Optional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Fornecedores_EmpresaId_Cnpj",
                table: "Fornecedores");

            migrationBuilder.AlterColumn<string>(
                name: "Cnpj",
                table: "Fornecedores",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(14)",
                oldMaxLength: 14);

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_EmpresaId_Cnpj",
                table: "Fornecedores",
                columns: new[] { "EmpresaId", "Cnpj" },
                unique: true,
                filter: "[Cnpj] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Fornecedores_EmpresaId_Cnpj",
                table: "Fornecedores");

            migrationBuilder.AlterColumn<string>(
                name: "Cnpj",
                table: "Fornecedores",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(14)",
                oldMaxLength: 14,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_EmpresaId_Cnpj",
                table: "Fornecedores",
                columns: new[] { "EmpresaId", "Cnpj" },
                unique: true);
        }
    }
}
