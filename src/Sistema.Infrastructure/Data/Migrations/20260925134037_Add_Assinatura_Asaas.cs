using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Assinatura_Asaas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AsaasCustomerId",
                table: "Assinaturas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsaasSubscriptionId",
                table: "Assinaturas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assinaturas_AsaasSubscriptionId",
                table: "Assinaturas",
                column: "AsaasSubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assinaturas_AsaasSubscriptionId",
                table: "Assinaturas");

            migrationBuilder.DropColumn(
                name: "AsaasCustomerId",
                table: "Assinaturas");

            migrationBuilder.DropColumn(
                name: "AsaasSubscriptionId",
                table: "Assinaturas");
        }
    }
}
